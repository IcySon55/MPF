﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BurnOutSharp;
using BurnOutSharp.External.psxt001z;
using BurnOutSharp.ProtectionType;
using MPF.Core.Data;

namespace MPF.Library
{
    public static class Protection
    {
        /// <summary>
        /// Run protection scan on a given dump environment
        /// </summary>
        /// <param name="path">Path to scan for protection</param>
        /// <param name="options">Options object that determines what to scan</param>
        /// <param name="progress">Optional progress callback</param>
        /// <returns>TCopy protection detected in the envirionment, if any</returns>
        public static async Task<(bool, string)> RunProtectionScanOnPath(string path, Options options, IProgress<ProtectionProgress> progress = null)
        {
            try
            {
                var found = await Task.Run(() =>
                {
                    var scanner = new Scanner(progress)
                    {
                        IncludeDebug = options.IncludeDebugProtectionInformation,
                        ScanAllFiles = options.ForceScanningForProtection,
                        ScanArchives = options.ScanArchivesForProtection,
                        ScanPackers = options.ScanPackersForProtection,
                    };
                    return scanner.GetProtections(path);
                });

                if (found == null || found.Count() == 0)
                    return (true, "None found");

                // Get an ordered list of distinct found protections
                var orderedDistinctProtections = found
                    .Where(kvp => kvp.Value != null && kvp.Value.Any())
                    .SelectMany(kvp => kvp.Value)
                    .Distinct()
                    .OrderBy(p => p);

                // Sanitize and join protections for writing
                string protections = SanitizeFoundProtections(orderedDistinctProtections);
                if (string.IsNullOrWhiteSpace(protections))
                    return (true, "None found");

                return (true, protections);
            }
            catch (Exception ex)
            {
                return (false, ex.ToString());
            }
        }

        /// <summary>
        /// Get the existence of an anti-modchip string from a PlayStation disc, if possible
        /// </summary>
        /// <param name="path">Path to scan for anti-modchip strings</param>
        /// <returns>Anti-modchip existence if possible, false on error</returns>
        public static async Task<bool> GetPlayStationAntiModchipDetected(string path)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var antiModchip = new PSXAntiModchip();
                    foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            byte[] fileContent = File.ReadAllBytes(file);
                            string protection = antiModchip.CheckContents(file, fileContent, false, null, null);
                            if (!string.IsNullOrWhiteSpace(protection))
                                return true;
                        }
                        catch { }
                    }
                }
                catch { }

                return false;
            });
        }

        /// <summary>
        /// Get if LibCrypt data is detected in the subchannel file, if possible
        /// </summary>
        /// <param name="sub">.sub file location</param>
        /// <returns>Status of the LibCrypt data, if possible</returns>
        public static bool? GetLibCryptDetected(string sub)
        {
            // If the file doesn't exist, we can't get info from it
            if (!File.Exists(sub))
                return null;

            return LibCrypt.CheckSubfile(sub);
        }

        /// <summary>
        /// Sanitize unnecessary protection duplication from output
        /// </summary>
        /// <param name="foundProtections">Enumerable of found protections</param>
        public static string SanitizeFoundProtections(IEnumerable<string> foundProtections)
        {
            // ActiveMARK
            if (foundProtections.Any(p => p == "ActiveMARK 5") && foundProtections.Any(p => p == "ActiveMARK"))
                foundProtections = foundProtections.Where(p => p != "ActiveMARK");

            // Cactus Data Shield
            if (foundProtections.Any(p => Regex.IsMatch(p, @"Cactus Data Shield [0-9]{3} .+")) && foundProtections.Any(p => p == "Cactus Data Shield 200"))
                foundProtections = foundProtections.Where(p => p != "Cactus Data Shield 200");

            // CD-Check
            foundProtections = foundProtections.Where(p => p != "Executable-Based CD Check");

            // CD-Cops
            if (foundProtections.Any(p => p == "CD-Cops") && foundProtections.Any(p => p.StartsWith("CD-Cops") && p.Length > "CD-Cops".Length))
                foundProtections = foundProtections.Where(p => p != "CD-Cops");

            // CD-Key / Serial
            foundProtections = foundProtections.Where(p => p != "CD-Key / Serial");

            // Electronic Arts
            if (foundProtections.Any(p => p == "EA CdKey Registration Module") && foundProtections.Any(p => p.StartsWith("EA CdKey Registration Module") && p.Length > "EA CdKey Registration Module".Length))
                foundProtections = foundProtections.Where(p => p != "EA CdKey Registration Module");
            if (foundProtections.Any(p => p == "EA DRM Protection") && foundProtections.Any(p => p.StartsWith("EA DRM Protection") && p.Length > "EA DRM Protection".Length))
                foundProtections = foundProtections.Where(p => p != "EA DRM Protection");

            // Games for Windows LIVE
            if (foundProtections.Any(p => p == "Games for Windows LIVE") && foundProtections.Any(p => p.StartsWith("Games for Windows LIVE") && !p.Contains("Zero Day Piracy Protection") && p.Length > "Games for Windows LIVE".Length))
                foundProtections = foundProtections.Where(p => p != "Games for Windows LIVE");

            // Impulse Reactor
            if (foundProtections.Any(p => p.StartsWith("Impulse Reactor Core Module")) && foundProtections.Any(p => p == "Impulse Reactor"))
                foundProtections = foundProtections.Where(p => p != "Impulse Reactor");

            // JoWood X-Prot
            if (foundProtections.Any(p => p.StartsWith("JoWood X-Prot")))
            {
                if (foundProtections.Any(p => Regex.IsMatch(p, @"JoWood X-Prot [0-9]\.[0-9]\.[0-9]\.[0-9]{2}")))
                {
                    foundProtections = foundProtections.Where(p => p != "JoWood X-Prot")
                        .Where(p => p != "JoWood X-Prot v1.0-v1.3")
                        .Where(p => p != "JoWood X-Prot v1.4+")
                        .Where(p => p != "JoWood X-Prot v2");
                }
                else if (foundProtections.Any(p => p == "JoWood X-Prot v2"))
                {
                    foundProtections = foundProtections.Where(p => p != "JoWood X-Prot")
                        .Where(p => p != "JoWood X-Prot v1.0-v1.3")
                        .Where(p => p != "JoWood X-Prot v1.4+");
                }
                else if (foundProtections.Any(p => p == "JoWood X-Prot v1.4+"))
                {
                    foundProtections = foundProtections.Where(p => p != "JoWood X-Prot")
                        .Where(p => p != "JoWood X-Prot v1.0-v1.3");
                }
                else if (foundProtections.Any(p => p == "JoWood X-Prot v1.0-v1.3"))
                {
                    foundProtections = foundProtections.Where(p => p != "JoWood X-Prot");
                }
            }

            // LaserLok
            // TODO: Figure this one out

            // Online Registration
            foundProtections = foundProtections.Where(p => !p.StartsWith("Executable-Based Online Registration"));

            // ProtectDISC / VOB ProtectCD/DVD
            // TODO: Figure this one out

            // SafeCast
            // TODO: Figure this one out

            // SafeDisc
            if (foundProtections.Any(p => p.StartsWith("SafeDisc")))
            {
                if (foundProtections.Any(p => Regex.IsMatch(p, @"SafeDisc [0-9]\.[0-9]{2}\.[0-9]{3}")))
                {
                    foundProtections = foundProtections.Where(p => p != "SafeDisc")
                        .Where(p => p != "SafeDisc 1/Lite")
                        .Where(p => p != "SafeDisc 1-3")
                        .Where(p => p != "SafeDisc 2")
                        .Where(p => p != "SafeDisc 3.20-4.xx (version removed)")
                        .Where(p => !p.StartsWith("SafeDisc (dplayerx.dll)"))
                        .Where(p => !p.StartsWith("SafeDisc (drvmgt.dll)"))
                        .Where(p => !p.StartsWith("SafeDisc (secdrv.sys)"))
                        .Where(p => p != "SafeDisc Lite");
                }
                else if (foundProtections.Any(p => p.StartsWith("SafeDisc (drvmgt.dll)")))
                {
                    foundProtections = foundProtections.Where(p => p != "SafeDisc")
                        .Where(p => p != "SafeDisc 1/Lite")
                        .Where(p => p != "SafeDisc 1-3")
                        .Where(p => p != "SafeDisc 2")
                        .Where(p => p != "SafeDisc 3.20-4.xx (version removed)")
                        .Where(p => !p.StartsWith("SafeDisc (dplayerx.dll)"))
                        .Where(p => !p.StartsWith("SafeDisc (secdrv.sys)"))
                        .Where(p => p != "SafeDisc Lite");
                }
                else if (foundProtections.Any(p => p.StartsWith("SafeDisc (secdrv.sys)")))
                {
                    foundProtections = foundProtections.Where(p => p != "SafeDisc")
                        .Where(p => p != "SafeDisc 1/Lite")
                        .Where(p => p != "SafeDisc 1-3")
                        .Where(p => p != "SafeDisc 2")
                        .Where(p => p != "SafeDisc 3.20-4.xx (version removed)")
                        .Where(p => !p.StartsWith("SafeDisc (dplayerx.dll)"))
                        .Where(p => p != "SafeDisc Lite");
                }
                else if (foundProtections.Any(p => p.StartsWith("SafeDisc (dplayerx.dll)")))
                {
                    foundProtections = foundProtections.Where(p => p != "SafeDisc")
                        .Where(p => p != "SafeDisc 1/Lite")
                        .Where(p => p != "SafeDisc 1-3")
                        .Where(p => p != "SafeDisc 2")
                        .Where(p => p != "SafeDisc 3.20-4.xx (version removed)")
                        .Where(p => p != "SafeDisc Lite");
                }
                else if (foundProtections.Any(p => p == "SafeDisc 3.20-4.xx (version removed)"))
                {
                    foundProtections = foundProtections.Where(p => p != "SafeDisc")
                        .Where(p => p != "SafeDisc 1/Lite")
                        .Where(p => p != "SafeDisc 1-3")
                        .Where(p => p != "SafeDisc 2")
                        .Where(p => p != "SafeDisc Lite");
                }
                else if (foundProtections.Any(p => p == "SafeDisc 2"))
                {
                    foundProtections = foundProtections.Where(p => p != "SafeDisc")
                        .Where(p => p != "SafeDisc 1/Lite")
                        .Where(p => p != "SafeDisc 1-3")
                        .Where(p => p != "SafeDisc Lite");
                }
                else if (foundProtections.Any(p => p == "SafeDisc 1/Lite"))
                {
                    foundProtections = foundProtections.Where(p => p != "SafeDisc")
                        .Where(p => p != "SafeDisc 1-3")
                        .Where(p => p != "SafeDisc Lite");
                }
                else if (foundProtections.Any(p => p == "SafeDisc Lite"))
                {
                    foundProtections = foundProtections.Where(p => p != "SafeDisc")
                        .Where(p => p != "SafeDisc 1-3");
                }
                else if (foundProtections.Any(p => p == "SafeDisc 1-3"))
                {
                    foundProtections = foundProtections.Where(p => p != "SafeDisc");
                }
            }

            // SecuROM
            // TODO: Figure this one out

            // SolidShield
            // TODO: Figure this one out

            // StarForce
            if (foundProtections.Any(p => p.StartsWith("StarForce")))
            {
                if (foundProtections.Any(p => Regex.IsMatch(p, @"StarForce [0-9]+\..+")))
                {
                    foundProtections = foundProtections.Where(p => p != "StarForce")
                        .Where(p => p != "StarForce 3-5")
                        .Where(p => p != "StarForce 5")
                        .Where(p => p != "StarForce 5 [Protected Module]");
                }
                else if (foundProtections.Any(p => p == "StarForce 5 [Protected Module]"))
                {
                    foundProtections = foundProtections.Where(p => p != "StarForce")
                        .Where(p => p != "StarForce 3-5")
                        .Where(p => p != "StarForce 5");
                }
                else if (foundProtections.Any(p => p == "StarForce 5"))
                {
                    foundProtections = foundProtections.Where(p => p != "StarForce")
                        .Where(p => p != "StarForce 3-5");
                }
                else if (foundProtections.Any(p => p == "StarForce 3-5"))
                {
                    foundProtections = foundProtections.Where(p => p != "StarForce");
                }
            }

            // Sysiphus
            if (foundProtections.Any(p => p == "Sysiphus") && foundProtections.Any(p => p.StartsWith("Sysiphus") && p.Length > "Sysiphus".Length))
                foundProtections = foundProtections.Where(p => p != "Sysiphus");

            // TAGES
            // TODO: Figure this one out

            // XCP
            if (foundProtections.Any(p => p == "XCP") && foundProtections.Any(p => p.StartsWith("XCP") && p.Length > "XCP".Length))
                foundProtections = foundProtections.Where(p => p != "XCP");

            return string.Join(", ", foundProtections);
        }
    }
}
