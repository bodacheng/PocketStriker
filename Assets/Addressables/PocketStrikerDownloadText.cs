using System.Globalization;
using UnityEngine;

public static class PocketStrikerDownloadText
{
    public static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return bytes.ToString(CultureInfo.InvariantCulture) + " B";

        double divisor;
        string unit;
        if (bytes < 1048576)
        {
            divisor = 1024d;
            unit = "KB";
        }
        else if (bytes < 1073741824)
        {
            divisor = 1048576d;
            unit = "MB";
        }
        else
        {
            divisor = 1073741824d;
            unit = "GB";
        }
        return (bytes / divisor).ToString("0.#", CultureInfo.InvariantCulture) + " " + unit;
    }

    public static string Confirmation(long bytes, SystemLanguage language)
    {
        var size = FormatSize(bytes);
        return language switch
        {
            SystemLanguage.Chinese => $"需要下载：{size}\n\n开始下载？",
            SystemLanguage.Japanese => $"ダウンロードサイズ：{size}\n\nダウンロードを開始しますか？",
            _ => $"Download size: {size}\n\nStart downloading?"
        };
    }

    public static string Progress(long downloadedBytes, long requiredBytes, SystemLanguage language)
    {
        var text = AddressablesResourcePolicy.DownloadProgressText(language);
        return $"{text}\n{FormatSize(downloadedBytes)} / {FormatSize(requiredBytes)}";
    }
}
