using System;

namespace Hazmat.Utilities.Models;

public class HazmatImporterSettings
{
    public ProcessModes Mode { get; set; }
    public string HazmatSection { get; set; } = string.Empty;
    public string SpecialProvisionSection { get; set; } = string.Empty;
    public string StartTag { get; set; }  = string.Empty;
    public int PauseDelay { get; set; }
    public int PauseInterval { get; set; }
}
