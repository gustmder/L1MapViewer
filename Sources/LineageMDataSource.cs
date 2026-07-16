using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Lin.Helper.Core.Dat;
using Lin.Helper.Core.Tile;

namespace L1MapViewer.Sources
{
    /// <summary>
    /// Read-only catalog for the ZIP-based DAT shards used by Lineage M.
    /// Map entries are returned as raw S32/SEG bytes. TI2 entries are
    /// Brotli-decoded by MDat and then converted to L1-compatible blocks.
    /// </summary>
    internal sealed class LineageMDataSource : IDisposable
    {
        private static readonly Regex MapEntryPattern = new Regex(
            @"^Map/([^/]+)/([0-9A-Fa-f]{8})\.(s32|seg)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex TileEntryPattern = new Regex(
            @"^Tile/([0-9]+)\.(ti2|til)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal sealed class ArchiveHandle : IDisposable
        {
            private readonly object _readLock = new object();

            public ArchiveHandle(string filePath)
            {
                FilePath = filePath;
                Dat = new MDat(filePath);
                if (Dat.Status != MDatStatus.Available)
                {
                    MDatStatus status = Dat.Status;
                    Dat.Dispose();
                    throw new InvalidDataException(
                        $"Unsupported DAT container ({status}): {filePath}");
                }
            }

            public string FilePath { get; }
            public MDat Dat { get; }

            public byte[] Extract(MDatEntry entry)
            {
                // MDat keeps one SharpZipLib ZipFile per archive. Serialize reads
                // within an archive while still allowing different shards in parallel.
                lock (_readLock)
                {
                    return Dat.Extract(entry);
                }
            }

            public void Dispose()
            {
                lock (_readLock)
                {
                    Dat.Dispose();
                }
            }
        }

        internal sealed class EntryReference
        {
            public ArchiveHandle Archive { get; init; }
            public MDatEntry Entry { get; init; }
            public string Extension { get; init; }
        }

        internal sealed class MapFileResource
        {
            public string MapId { get; init; }
            public string LogicalPath { get; init; }
            public string FileName { get; init; }
            public bool IsS32 { get; init; }
            internal EntryReference Source { get; init; }

            private MapFileResource()
            {
            }

            internal static MapFileResource Create(
                string mapId,
                string logicalPath,
                string fileName,
                bool isS32,
                EntryReference source)
            {
                return new MapFileResource
                {
                    MapId = mapId,
                    LogicalPath = logicalPath,
                    FileName = fileName,
                    IsS32 = isS32,
                    Source = source
                };
            }
        }

        private readonly List<ArchiveHandle> _archives = new List<ArchiveHandle>();
        private readonly List<MapFileResource> _mapFiles = new List<MapFileResource>();
        private readonly Dictionary<string, EntryReference> _mapEntries =
            new Dictionary<string, EntryReference>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, EntryReference> _tileEntries =
            new Dictionary<int, EntryReference>();
        private int _firstTileLogged;
        private bool _disposed;

        public LineageMDataSource(string rootPath)
        {
            RootPath = Path.GetFullPath(rootPath);
            try
            {
                BuildMapCatalog();
                BuildTileCatalog();

                if (_mapFiles.Count == 0)
                    throw new InvalidDataException($"No Lineage M map entries found in: {RootPath}");
                if (_tileEntries.Count == 0)
                    throw new InvalidDataException($"No Lineage M tile entries found in: {RootPath}");
            }
            catch
            {
                Dispose();
                throw;
            }

            DebugLog.Log(
                $"[LineageMDataSource] Ready: maps={_mapFiles.Select(f => f.MapId).Distinct(StringComparer.OrdinalIgnoreCase).Count()}, " +
                $"mapFiles={_mapFiles.Count}, tiles={_tileEntries.Count}, archives={_archives.Count}");
        }

        public string RootPath { get; }
        public IReadOnlyList<MapFileResource> MapFiles => _mapFiles;
        public int TileCount => _tileEntries.Count;

        public static bool IsSupportedFolder(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
                return false;

            bool hasMap = EnumerateShardPaths(rootPath, "Map").Any();
            bool hasTile = EnumerateShardPaths(rootPath, "Tile").Any();
            return hasMap && hasTile;
        }

        public bool ContainsMapFile(string logicalPath)
        {
            return !string.IsNullOrEmpty(logicalPath) &&
                   _mapEntries.ContainsKey(NormalizeEntryPath(logicalPath));
        }

        public bool ContainsTile(int tileId)
        {
            return _tileEntries.ContainsKey(tileId);
        }

        public byte[] ReadMapFile(string logicalPath)
        {
            ThrowIfDisposed();

            string key = NormalizeEntryPath(logicalPath);
            if (!_mapEntries.TryGetValue(key, out var source))
                throw new FileNotFoundException($"Lineage M map entry not found: {logicalPath}");

            return source.Archive.Extract(source.Entry);
        }

        public List<byte[]> LoadTileBlocks(int tileId)
        {
            ThrowIfDisposed();

            if (!_tileEntries.TryGetValue(tileId, out var source))
                return null;

            byte[] data = source.Archive.Extract(source.Entry);
            List<byte[]> blocks;
            if (source.Extension.Equals(".ti2", StringComparison.OrdinalIgnoreCase))
                blocks = MTil.ConvertToL1Til(data).ToList();
            else
                blocks = L1Til.Parse(data);

            if (Interlocked.Exchange(ref _firstTileLogged, 1) == 0)
            {
                DebugLog.Log(
                    $"[LineageMDataSource] First tile loaded: id={tileId}, " +
                    $"format={source.Extension}, blocks={blocks?.Count ?? 0}");
            }

            return blocks;
        }

        public byte[] ReadTileFile(int tileId)
        {
            List<byte[]> blocks = LoadTileBlocks(tileId);
            return blocks == null ? null : L1Til.BuildTil(blocks);
        }

        private void BuildMapCatalog()
        {
            // Keyed by map + block stem so S32 can take precedence over SEG.
            var selected = new Dictionary<string, MapFileResource>(StringComparer.OrdinalIgnoreCase);

            foreach (string archivePath in EnumerateShardPaths(RootPath, "Map"))
            {
                ArchiveHandle archive = TryOpenArchive(archivePath);
                if (archive == null)
                    continue;

                foreach (MDatEntry entry in archive.Dat.Entries)
                {
                    string logicalPath = NormalizeEntryPath(entry.FileName);
                    Match match = MapEntryPattern.Match(logicalPath);
                    if (!match.Success)
                        continue;

                    string mapId = match.Groups[1].Value;
                    string blockStem = match.Groups[2].Value.ToLowerInvariant();
                    string extension = "." + match.Groups[3].Value.ToLowerInvariant();
                    bool isS32 = extension == ".s32";
                    string selectionKey = mapId + "\0" + blockStem;

                    var source = new EntryReference
                    {
                        Archive = archive,
                        Entry = entry,
                        Extension = extension
                    };
                    var candidate = MapFileResource.Create(
                        mapId,
                        logicalPath,
                        Path.GetFileName(logicalPath),
                        isS32,
                        source);

                    if (selected.TryGetValue(selectionKey, out var existing) && existing.IsS32 && !isS32)
                        continue;

                    // Later shards replace earlier entries of the same format. An
                    // S32 always replaces a SEG with the same block coordinates.
                    selected[selectionKey] = candidate;
                }
            }

            _mapFiles.AddRange(selected.Values
                .OrderBy(f => f.MapId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(f => f.FileName, StringComparer.OrdinalIgnoreCase));

            foreach (MapFileResource mapFile in _mapFiles)
                _mapEntries[NormalizeEntryPath(mapFile.LogicalPath)] = mapFile.Source;
        }

        private void BuildTileCatalog()
        {
            foreach (string archivePath in EnumerateShardPaths(RootPath, "Tile"))
            {
                ArchiveHandle archive = TryOpenArchive(archivePath);
                if (archive == null)
                    continue;

                foreach (MDatEntry entry in archive.Dat.Entries)
                {
                    string logicalPath = NormalizeEntryPath(entry.FileName);
                    Match match = TileEntryPattern.Match(logicalPath);
                    if (!match.Success || !int.TryParse(match.Groups[1].Value, out int tileId))
                        continue;

                    string extension = "." + match.Groups[2].Value.ToLowerInvariant();
                    var candidate = new EntryReference
                    {
                        Archive = archive,
                        Entry = entry,
                        Extension = extension
                    };

                    if (_tileEntries.TryGetValue(tileId, out var existing) &&
                        existing.Extension.Equals(".ti2", StringComparison.OrdinalIgnoreCase) &&
                        extension == ".til")
                    {
                        continue;
                    }

                    // Prefer the native M TI2 when both TI2 and legacy TIL exist.
                    _tileEntries[tileId] = candidate;
                }
            }
        }

        private ArchiveHandle TryOpenArchive(string archivePath)
        {
            try
            {
                var archive = new ArchiveHandle(archivePath);
                _archives.Add(archive);
                return archive;
            }
            catch (Exception ex)
            {
                DebugLog.Log($"[LineageMDataSource] Skipping {Path.GetFileName(archivePath)}: {ex.Message}");
                return null;
            }
        }

        private static IEnumerable<string> EnumerateShardPaths(string rootPath, string prefix)
        {
            if (!Directory.Exists(rootPath))
                return Enumerable.Empty<string>();

            var pattern = new Regex(
                "^" + Regex.Escape(prefix) + @"([0-9]+)\.dat$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            return Directory.EnumerateFiles(rootPath, "*", SearchOption.TopDirectoryOnly)
                .Select(path => new
                {
                    Path = path,
                    Match = pattern.Match(Path.GetFileName(path))
                })
                .Where(item => item.Match.Success)
                .OrderBy(item => int.Parse(item.Match.Groups[1].Value))
                .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                .Select(item => item.Path)
                .ToArray();
        }

        private static string NormalizeEntryPath(string path)
        {
            return path.Replace('\\', '/').TrimStart('/');
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LineageMDataSource));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            foreach (ArchiveHandle archive in _archives)
                archive.Dispose();

            _archives.Clear();
            _disposed = true;
        }
    }
}
