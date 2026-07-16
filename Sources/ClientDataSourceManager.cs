using System;
using System.Collections.Generic;
using System.IO;

namespace L1MapViewer.Sources
{
    internal enum ClientDataSourceKind
    {
        Unknown,
        Classic,
        LineageM
    }

    /// <summary>
    /// Keeps the selected client source and provides a small compatibility
    /// facade while the rest of the application still uses physical paths.
    /// </summary>
    internal static class ClientDataSourceManager
    {
        private static readonly object SyncRoot = new object();
        private static LineageMDataSource _lineageMSource;
        private static string _rootPath = string.Empty;
        private static ClientDataSourceKind _kind = ClientDataSourceKind.Unknown;

        public static ClientDataSourceKind Kind
        {
            get
            {
                lock (SyncRoot)
                    return _kind;
            }
        }

        public static bool IsLineageM => Kind == ClientDataSourceKind.LineageM;
        public static bool IsReadOnly => IsLineageM;

        public static LineageMDataSource LineageMSource
        {
            get
            {
                lock (SyncRoot)
                    return _lineageMSource;
            }
        }

        public static ClientDataSourceKind Detect(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
                return ClientDataSourceKind.Unknown;

            if (Directory.Exists(Path.Combine(rootPath, "map")))
                return ClientDataSourceKind.Classic;

            return LineageMDataSource.IsSupportedFolder(rootPath)
                ? ClientDataSourceKind.LineageM
                : ClientDataSourceKind.Unknown;
        }

        public static bool IsSupportedFolder(string rootPath)
        {
            return Detect(rootPath) != ClientDataSourceKind.Unknown;
        }

        public static void EnsureOpen(string rootPath)
        {
            string fullPath = Path.GetFullPath(rootPath);
            ClientDataSourceKind detected = Detect(fullPath);
            if (detected == ClientDataSourceKind.Unknown)
                throw new InvalidDataException($"Unsupported Lineage client folder: {rootPath}");

            lock (SyncRoot)
            {
                if (_kind == detected &&
                    string.Equals(_rootPath, fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _lineageMSource?.Dispose();
                _lineageMSource = null;

                if (detected == ClientDataSourceKind.LineageM)
                    _lineageMSource = new LineageMDataSource(fullPath);

                _rootPath = fullPath;
                _kind = detected;
                DebugLog.Log($"[ClientDataSourceManager] Opened {detected}: {fullPath}");
            }
        }

        public static bool MapFileExists(string resourcePath)
        {
            LineageMDataSource source = LineageMSource;
            return IsLineageM
                ? source != null && source.ContainsMapFile(resourcePath)
                : File.Exists(resourcePath);
        }

        public static byte[] ReadMapFile(string resourcePath)
        {
            LineageMDataSource source = LineageMSource;
            return IsLineageM
                ? source.ReadMapFile(resourcePath)
                : File.ReadAllBytes(resourcePath);
        }

        public static bool TileExists(int tileId)
        {
            LineageMDataSource source = LineageMSource;
            return IsLineageM && source != null && source.ContainsTile(tileId);
        }

        public static List<byte[]> LoadTileBlocks(int tileId)
        {
            LineageMDataSource source = LineageMSource;
            if (!IsLineageM || source == null)
                return null;

            try
            {
                return source.LoadTileBlocks(tileId);
            }
            catch (Exception ex)
            {
                DebugLog.Log($"[ClientDataSourceManager] Failed to load M tile {tileId}: {ex.Message}");
                return null;
            }
        }

        public static byte[] ReadTileFile(int tileId)
        {
            LineageMDataSource source = LineageMSource;
            if (!IsLineageM || source == null)
                return null;

            return source.ReadTileFile(tileId);
        }

        public static void DisposeCurrent()
        {
            lock (SyncRoot)
            {
                _lineageMSource?.Dispose();
                _lineageMSource = null;
                _rootPath = string.Empty;
                _kind = ClientDataSourceKind.Unknown;
            }
        }
    }
}
