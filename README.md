# L1MapViewer
這是一個可以預覽線上遊戲客戶端地圖資源檔的工具
>其中用到的檔案加解密算法多為社群中大神們的分享/討論出來的

大概的流程是:
 - 讀取地圖檔
 - 從地圖檔獲得所需的圖形資源檔以及對應的位置
 - 從客戶端的壓縮檔中將這些圖形資源檔取出
 - 取出的圖形資源檔轉成bmp 
 - 拼成一張地圖

實際效果請參考影片https://youtu.be/UVRn_-dOmng


# update log
## 20251126
* update to net7.0 project
* fix project files 
* ready to build