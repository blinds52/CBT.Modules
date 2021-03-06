﻿using Microsoft.Build.Framework;
using NuGet.Configuration;
using NuGet.Packaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace CBT.NuGet.Internal
{
    /// <inheritdoc />
    /// <summary>
    /// Represents a class that can parse a NuGet packages.config file.
    /// </summary>
    internal sealed class NuGetPackagesConfigParser : NuGetPackageConfigParserBase
    {
        /// <summary>
        /// The name of this package configuration file which is packages.config.
        /// </summary>
        public const string PackageConfigFilename = "packages.config";

        public NuGetPackagesConfigParser(ISettings settings, CBTTaskLogHelper log)
            : base(settings, log)
        {
        }

        public static bool IsPackagesConfigFile(string packageConfigPath)
        {
            return packageConfigPath.EndsWith(PackageConfigFilename, StringComparison.OrdinalIgnoreCase);
        }

        public override bool TryGetPackages(string packageConfigPath, PackageRestoreData packageRestoreData, out IEnumerable<PackageIdentityWithPath> packages)
        {
            packages = null;


            // NuGet sets the project restore style to "Unknown" if its not PackageReference or ProjectJson
            //
            if (String.Equals("Unknown", packageRestoreData?.RestoreProjectStyle))
            {
                // Attempt to check if there's a packages.config next to the project
                //
                packageConfigPath = Path.Combine(Path.GetDirectoryName(packageConfigPath), PackageConfigFilename);

                if (!File.Exists(packageConfigPath))
                {
                    return false;
                }
            }

            if (!IsPackagesConfigFile(packageConfigPath))
            {
                return false;
            }

            string repositoryPath = SettingsUtility.GetRepositoryPath(NuGetSettings);

            if (String.IsNullOrWhiteSpace(repositoryPath))
            {
                throw new NuGetConfigurationException("Unable to determine the NuGet repository path.  Ensure that you are you specifying a path in your NuGet.config (https://docs.microsoft.com/en-us/nuget/schema/nuget-config-file#config-section).");
            }

            repositoryPath = Path.GetFullPath(repositoryPath);

            if (!Directory.Exists(repositoryPath))
            {
                throw new DirectoryNotFoundException($"The NuGet repository '{repositoryPath}' does not exist.  Ensure that NuGet is restore packages to the location specified in your NuGet.config.");
            }

            Log.LogMessage(MessageImportance.Low, $"Using repository path: '{repositoryPath}'");

            PackagePathResolver packagePathResolver = new PackagePathResolver(repositoryPath);

            XDocument document = XDocument.Load(packageConfigPath);

            PackagesConfigReader packagesConfigReader = new PackagesConfigReader(document);

            packages = packagesConfigReader.GetPackages(allowDuplicatePackageIds: true)
                .OrderBy(i => i.PackageIdentity.Id)
                .ThenBy(i => i.PackageIdentity.Version)
                .Select(i =>
                {
                    string installPath = packagePathResolver.GetInstallPath(i.PackageIdentity);

                    if (!String.IsNullOrWhiteSpace(installPath))
                    {
                        installPath = Path.GetFullPath(installPath);
                    }
                    else
                    {
                        Log.LogWarning($"The package '{i.PackageIdentity.Id}' was not found in the repository.");
                    }

                    return new PackageIdentityWithPath(i.PackageIdentity, installPath);
                })
                .Where(i => !String.IsNullOrWhiteSpace(i.FullPath));

            return true;
        }
    }
}