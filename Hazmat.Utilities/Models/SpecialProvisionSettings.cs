using System;

namespace Hazmat.Utilities.Models;

public class SpecialProvisionSettings
{
    public ProcessModes Mode { get; set; }
    public string SpecialProvisionSection { get; set; } = string.Empty;
    public string StartTag { get; set; }  = string.Empty;
    public int PauseDelay { get; set; }
    public int PauseInterval { get; set; }

}
