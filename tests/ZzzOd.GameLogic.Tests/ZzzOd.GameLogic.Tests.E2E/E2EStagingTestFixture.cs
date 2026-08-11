using System.Security.Cryptography;
using System.Text.Json;
using System.Runtime.InteropServices;
using ZzzOd.AppHost.Resources;

namespace ZzzOd.GameLogic.Tests.E2E;

internal static class E2EStagingTestFixture
{
	public static void CreateManifest(string runRoot)
	{
		const string relativePath = "assets/e2e-manifest-marker.yml";
		string markerPath = Path.Combine(runRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
		Directory.CreateDirectory(Path.GetDirectoryName(markerPath)!);
		File.WriteAllText(markerPath, "marker: e2e");
		ZzzAssetManifest manifest = new()
		{
			SchemaVersion = ZzzAssetManifest.CurrentSchemaVersion,
			Rid = RuntimeInformation.RuntimeIdentifier,
			GeneratedSource = "test-staging",
			SourceSummary = "E2E-TEST-STAGING",
			ManagedRoots = ["assets"],
			Files =
			[
				new ZzzAssetManifestFile
				{
					Path = relativePath,
					Category = "static-assets",
					Required = true,
					Rids = [RuntimeInformation.RuntimeIdentifier],
					Size = new FileInfo(markerPath).Length,
					Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(markerPath))),
					GeneratedSource = "test-staging",
				},
			],
		};
		File.WriteAllText(Path.Combine(runRoot, "assets-manifest.json"), JsonSerializer.Serialize(manifest));
	}
}
