using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using BepInEx.Logging;
using System.Text;

namespace BoomBoxCartMod.Util
{
	public static class YoutubeDL
	{
		private static BoomBoxCartMod Instance = BoomBoxCartMod.instance;
		private static ManualLogSource Logger => Instance.logger;

		private static readonly string baseFolder = Path.Combine(Directory.GetCurrentDirectory(), "BoomboxedCart");
		private const string YtDLP_URL = "https://github.com/yt-dlp/yt-dlp/releases/download/2025.02.19/yt-dlp.exe";
		private const string FFMPEG_URL = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
		private static readonly string ytDlpPath = Path.Combine(baseFolder, "yt-dlp.exe");
		private static readonly string ffmpegFolder = Path.Combine(baseFolder, "ffmpeg");
		private static string ffmpegBinPath = Path.Combine(ffmpegFolder, "ffmpeg-master-latest-win64-gpl", "bin", "ffmpeg.exe");

		public static async Task InitializeAsync()
		{
			if (!Directory.Exists(baseFolder))
			{
				Directory.CreateDirectory(baseFolder);
			}

			if (!File.Exists(ytDlpPath))
			{
				Console.WriteLine("yt-dlp not found. Downloading...");
				await DownloadFileAsync(YtDLP_URL, ytDlpPath);
			}

			bool needsFFmpeg = !File.Exists(ffmpegBinPath);
			if (!needsFFmpeg && !Directory.Exists(Path.GetDirectoryName(ffmpegBinPath)))
			{
				needsFFmpeg = true;
			}

			if (needsFFmpeg)
			{
				Console.WriteLine("ffmpeg not found. Downloading and extracting...");
				await DownloadAndExtractFFmpegAsync();
			}

			if (!File.Exists(ffmpegBinPath))
			{
				throw new Exception($"ffmpeg executable was not found at {ffmpegBinPath} after extraction. Internet problem? Not on Windows problem?");
			}

			Console.WriteLine("Initialization complete.");
		}

		private static async Task DownloadFileAsync(string url, string destinationPath)
		{
			using HttpClient client = new();
			byte[] data = await client.GetByteArrayAsync(url);
			File.WriteAllBytes(destinationPath, data);
		}

		private static async Task DownloadAndExtractFFmpegAsync()
		{
			string zipPath = Path.Combine(baseFolder, "ffmpeg.zip");

			try
			{
				if (Directory.Exists(ffmpegFolder))
				{
					try
					{
						Directory.Delete(ffmpegFolder, true);
						Directory.CreateDirectory(ffmpegFolder);
					}
					catch (Exception ex)
					{
						Console.WriteLine($"Warning: Failed to clean ffmpeg folder: {ex.Message}");
					}
				}

				Console.WriteLine($"Downloading FFmpeg from {FFMPEG_URL}...");
				await DownloadFileAsync(FFMPEG_URL, zipPath);

				if (!File.Exists(zipPath))
				{
					throw new Exception("FFmpeg zip file not downloaded properly.");
				}

				Console.WriteLine($"Downloaded FFmpeg zip file ({new FileInfo(zipPath).Length} bytes). Extracting...");

				ZipFile.ExtractToDirectory(zipPath, ffmpegFolder);

				File.Delete(zipPath);

				if (!File.Exists(ffmpegBinPath))
				{
					Console.WriteLine($"FFmpeg not found at expected path: {ffmpegBinPath}. Maaaaaaaaan");
					Console.WriteLine("Searching for ffmpeg.exe in extracted files...");

					string[] ffmpegFiles = Directory.GetFiles(ffmpegFolder, "ffmpeg.exe", SearchOption.AllDirectories);

					if (ffmpegFiles.Length > 0)
					{
						string newPath = ffmpegFiles[0];
						Console.WriteLine($"Found ffmpeg.exe at: {newPath}");

						ffmpegBinPath = newPath;
					}
					else
					{
						Console.WriteLine("No ffmpeg.exe found in the extracted files.");
						throw new Exception("ffmpeg.exe not found in extracted files. Uh oh!");
					}
				}

				Console.WriteLine("FFmpeg extracted successfully.");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error downloading or extracting FFmpeg: {ex.Message}");
				throw;
			}
		}

		public static async Task<(string filePath, string title)> DownloadAudioWithTitleAsync(string videoUrl)
		{
			await InitializeAsync();

			string tempFolder = Path.Combine(baseFolder, Guid.NewGuid().ToString());
			Directory.CreateDirectory(tempFolder);

			//Logger.LogInfo($"Getting title and downloading audio for {videoUrl}...");

			return await Task.Run(async () =>
			{
				try
				{
					string title = await GetVideoTitleInternalAsync(videoUrl);
					if (string.IsNullOrEmpty(title))
					{
						title = "Unknown Title";
					}

					Logger.LogInfo($"Got video title downloaded: {title}");

					string noIckySpecialCharsFileName = $"audio_{DateTime.Now.Ticks}.%(ext)s";
					string command = $"-x --audio-format mp3 --audio-quality 192K --ffmpeg-location \"{ffmpegBinPath}\" --output \"{Path.Combine(tempFolder, noIckySpecialCharsFileName)}\" {videoUrl}";


					ProcessStartInfo processInfo = new()
					{
						FileName = ytDlpPath,
						Arguments = command,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						UseShellExecute = false,
						CreateNoWindow = true,
						StandardOutputEncoding = Encoding.UTF8
					};

					using (Process process = Process.Start(processInfo))
					{
						if (process == null)
						{
							throw new Exception("Failed to start yt-dlp process.");
						}

						process.WaitForExit();

						if (process.ExitCode != 0)
						{
							string error = process.StandardError.ReadToEnd();
							throw new Exception($"yt-dlp error: {error}");
						}
					}

					string audioFilePath = Directory.GetFiles(tempFolder, "*.mp3").FirstOrDefault();
					if (audioFilePath == null)
					{
						throw new Exception("Audio download failed.");
					}

					return (audioFilePath, title);
				}
				catch (Exception ex)
				{
					if (Directory.Exists(tempFolder))
					{
						Directory.Delete(tempFolder, true);
					}
					throw new Exception($"Error downloading audio: {ex.Message}");
				}
			});
		}

		private static async Task<string> GetVideoTitleInternalAsync(string url)
		{
			try
			{
				await InitializeAsync();

				ProcessStartInfo psi = new ProcessStartInfo
				{
					FileName = ytDlpPath,
					Arguments = $"--get-title {url}",
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true
				};

				//Logger.LogInfo($"Running yt-dlp to get title for: {url}");

				using (var process = new Process { StartInfo = psi })
				{
					var tcs = new TaskCompletionSource<string>();
					process.Start();

					string title = await process.StandardOutput.ReadToEndAsync();
					await process.StandardError.ReadToEndAsync();

					var timeoutTask = Task.Delay(10000);
					var processExitTask = Task.Run(() => process.WaitForExit());

					if (await Task.WhenAny(processExitTask, timeoutTask) == timeoutTask)
					{
						try { process.Kill(); } catch { }
						Logger.LogWarning("yt-dlp title fetch timed out");
						return "Unknown Title (Timeout)";
					}

					if (process.ExitCode != 0)
					{
						Logger.LogError($"yt-dlp error code: {process.ExitCode}");
						return "Unknown Title";
					}

					//title = title.Trim();
					//byte[] bytes = Encoding.Default.GetBytes(title);
					//title = Encoding.UTF8.GetString(bytes);

					Logger.LogInfo($"Got video title: {title}");

					if (!string.IsNullOrEmpty(title))
					{
						try
						{
							// trying to clean title
							title = new string(title.Where(c => !char.IsControl(c) || c == '\n' || c == '\r' || c == '\t').ToArray());
						}
						catch (Exception ex)
						{
							Logger.LogWarning($"Error sanitizing title: {ex.Message}");
						}
					}

					return string.IsNullOrEmpty(title) ? "Unknown Title" : title;
				}
			}
			catch (Exception ex)
			{
				Logger.LogError($"Error getting video title: {ex.Message}");
				return "Unknown Title";
			}
		}
	}
}