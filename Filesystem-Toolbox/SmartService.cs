using System;
using System.IO;
using System.Management;

namespace Filesystem_Toolbox {

  internal enum SmartStatus {
    Ok,
    PredictingFailure,

    /// <summary>The medium does not expose SMART (typical for USB sticks and SD cards) or WMI failed.</summary>
    Unavailable,
  }

  /// <summary>
  /// Best-effort SMART readout via WMI: watched root - drive letter - physical disk -
  /// MSStorageDriver_FailurePredictStatus. Every failure path collapses to
  /// <see cref="SmartStatus.Unavailable"/> - most removable media simply have no SMART.
  /// </summary>
  internal static class SmartService {

    public static SmartStatus ForRoot(DirectoryInfo root) {
      if (root == null)
        return SmartStatus.Unavailable;

      try {
        var driveLetter = Path.GetPathRoot(root.FullName)?.TrimEnd('\\', '/');
        if (string.IsNullOrEmpty(driveLetter) || driveLetter.Length != 2 || driveLetter[1] != ':')
          return SmartStatus.Unavailable;

        // logical disk -> partition -> physical drive
        string physicalDeviceId = null;
        using (var partitionSearcher = new ManagementObjectSearcher($"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{driveLetter}'}} WHERE AssocClass=Win32_LogicalDiskToPartition"))
          foreach (ManagementObject partition in partitionSearcher.Get())
            using (var driveSearcher = new ManagementObjectSearcher($"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition"))
              foreach (ManagementObject drive in driveSearcher.Get())
                physicalDeviceId = drive["PNPDeviceID"]?.ToString();

        if (physicalDeviceId == null)
          return SmartStatus.Unavailable;

        // the failure-prediction instance name starts with the PNP device id
        using (var smartSearcher = new ManagementObjectSearcher(@"root\wmi", "SELECT InstanceName, PredictFailure FROM MSStorageDriver_FailurePredictStatus"))
          foreach (ManagementObject status in smartSearcher.Get()) {
            var instance = status["InstanceName"]?.ToString();
            if (instance == null || instance.IndexOf(physicalDeviceId.Replace('\\', '_'), StringComparison.OrdinalIgnoreCase) < 0
                && instance.IndexOf(physicalDeviceId, StringComparison.OrdinalIgnoreCase) < 0)
              continue;

            return (bool)status["PredictFailure"] ? SmartStatus.PredictingFailure : SmartStatus.Ok;
          }

        return SmartStatus.Unavailable;
      } catch (Exception) {
        return SmartStatus.Unavailable;
      }
    }

  }
}
