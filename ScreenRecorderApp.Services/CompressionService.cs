using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;
using Xabe.FFmpeg.Events;

namespace ScreenRecorderApp.Services;

public static class CompressionService
{
	private static readonly string FfmpegDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenRecorderApp", "ffmpeg");

	private static bool _ffmpegReady;

	public static async Task<string> CompressAsync(string inputPath, IProgress<(long downloaded, long total)>? onDownloadProgress = null, IProgress<double>? onCompressProgress = null, CancellationToken ct = default(CancellationToken))
	{
		await EnsureFfmpegAsync(onDownloadProgress, ct);
		string outputPath = BuildOutputPath(inputPath);
		double totalSeconds = (await FFmpeg.GetMediaInfo(inputPath, ct)).Duration.TotalSeconds;
		IConversion conversion = FFmpeg.Conversions.New().AddParameter("-i \"" + inputPath + "\"").AddParameter("-c:v libx264")
			.AddParameter("-crf 28")
			.AddParameter("-preset fast")
			.AddParameter("-pix_fmt yuv420p")
			.AddParameter("-c:a aac")
			.AddParameter("-b:a 96k")
			.AddParameter("-movflags +faststart")
			.AddParameter("\"" + outputPath + "\"");
		conversion.OnProgress += delegate(object _, ConversionProgressEventArgs args)
		{
			double value = ((totalSeconds > 0.0) ? Math.Clamp(args.Duration.TotalSeconds / totalSeconds, 0.0, 1.0) : 0.0);
			onCompressProgress?.Report(value);
		};
		await conversion.Start(ct);
		File.Delete(inputPath);
		return outputPath;
	}

	private static async Task EnsureFfmpegAsync(IProgress<(long, long)>? downloadProgress, CancellationToken ct)
	{
		if (_ffmpegReady)
		{
			return;
		}
		Directory.CreateDirectory(FfmpegDir);
		FFmpeg.SetExecutablesPath(FfmpegDir);
		if (File.Exists(Path.Combine(FfmpegDir, "ffmpeg.exe")))
		{
			_ffmpegReady = true;
			return;
		}
		await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, FfmpegDir, new Progress<ProgressInfo>(delegate(ProgressInfo info)
		{
			downloadProgress?.Report((info.DownloadedBytes, info.TotalBytes));
		}));
		_ffmpegReady = true;
	}

	private static string BuildOutputPath(string inputPath)
	{
		string? directoryName = Path.GetDirectoryName(inputPath);
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(inputPath);
		return Path.Combine(directoryName, fileNameWithoutExtension + "_c.mp4");
	}
}
