﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ExtensionGallery.Code
{
	public class PackageHelper
	{
		private string _webroot;
		private string _extensionRoot;
		public static List<Package> _cache;

		public PackageHelper(string webroot)
		{
			_webroot = webroot;
			_extensionRoot = Path.Combine(webroot, "extensions");
		}

		public List<Package> PackageCache
		{
			get
			{
				if (_cache == null)
					_cache = GetAllPackages();

				return _cache;
			}
		}

		private List<Package> GetAllPackages()
		{
			List<Package> packages = new List<Package>();

            if(!Directory.Exists(_extensionRoot))
            {
                return packages.ToList();
            }

			foreach (string extension in Directory.EnumerateDirectories(_extensionRoot))
			{
				string json = Path.Combine(extension, "extension.json");
				if (File.Exists(json))
				{
					string content = File.ReadAllText(json);
					Package package = JsonConvert.DeserializeObject(content, typeof(Package)) as Package;
					packages.Add(package);
				}
			}

			return packages.OrderByDescending(p => p.DatePublished).ToList();
		}

		public Package GetPackage(string id)
		{
			if (PackageCache.Any(p => p.ID == id))
			{
				return PackageCache.SingleOrDefault(p => p.ID == id);
			}

			string folder = Path.Combine(_extensionRoot, id);
			List<Package> packages = new List<Package>();

			return DeserializePackage(folder);
		}

		private static Package DeserializePackage(string version)
		{
			string content = File.ReadAllText(Path.Combine(version, "extension.json"));
			return JsonConvert.DeserializeObject(content, typeof(Package)) as Package;
		}

		public async Task<Package> ProcessVsix(Stream vsixStream, string repo, string issuetracker)
		{
			string tempFolder = Path.Combine(_webroot, "temp", Guid.NewGuid().ToString());

			try
			{
				string tempVsix = Path.Combine(tempFolder, "extension.vsix");

				if (!Directory.Exists(tempFolder))
					Directory.CreateDirectory(tempFolder);

				using (FileStream fileStream = File.Create(tempVsix))
				{
					await vsixStream.CopyToAsync(fileStream);
				}

				ZipFile.ExtractToDirectory(tempVsix, tempFolder);

				VsixManifestParser parser = new VsixManifestParser();
				Package package = parser.CreateFromManifest(tempFolder, repo, issuetracker);

				//if (PackageCache.Any(p => p.ID == package.ID && new Version(p.Version) > new Version(package.Version)))
				//	throw new ArgumentException("The VSIX version (" + package.Version + ") must be equal or higher than the existing VSIX");

				string vsixFolder = Path.Combine(_extensionRoot, package.ID);

				SavePackage(tempFolder, package, vsixFolder);

				File.Copy(tempVsix, Path.Combine(vsixFolder, "extension.vsix"), true);

				return package;
			}
			finally
			{
				Directory.Delete(tempFolder, true);
			}
		}

		private void SavePackage(string tempFolder, Package package, string vsixFolder)
		{
			if (Directory.Exists(vsixFolder))
				Directory.Delete(vsixFolder, true);

			Directory.CreateDirectory(vsixFolder);

			string icon = Path.Combine(tempFolder, package.Icon ?? string.Empty);
			if (File.Exists(icon))
			{
				File.Copy(icon, Path.Combine(vsixFolder, "icon-" + package.Version + ".png"), true);
				package.Icon = "icon-" + package.Version + ".png";
            }

			string preview = Path.Combine(tempFolder, package.Preview ?? string.Empty);
			if (File.Exists(preview))
			{
				File.Copy(preview, Path.Combine(vsixFolder, "preview-" + package.Version + ".png"), true);
				package.Preview = "preview-" + package.Version + ".png";
			}

			string json = JsonConvert.SerializeObject(package);

			File.WriteAllText(Path.Combine(vsixFolder, "extension.json"), json, Encoding.UTF8);

			Package existing = PackageCache.FirstOrDefault(p => p.ID == package.ID);

			if (PackageCache.Contains(existing))
			{
				PackageCache.Remove(existing);
			}

			PackageCache.Add(package);
		}
	}
}