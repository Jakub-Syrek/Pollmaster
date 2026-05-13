namespace Pollmaster.Shared.Contracts;

/// <summary>
/// Polish national air quality index level published by GIOŚ.
/// </summary>
public enum AirQualityIndexLevel
{
    /// <summary>No reading available.</summary>
    Unknown = -1,

    /// <summary>Very good (Bardzo dobry).</summary>
    VeryGood = 0,

    /// <summary>Good (Dobry).</summary>
    Good = 1,

    /// <summary>Moderate (Umiarkowany).</summary>
    Moderate = 2,

    /// <summary>Sufficient (Dostateczny).</summary>
    Sufficient = 3,

    /// <summary>Bad (Zły).</summary>
    Bad = 4,

    /// <summary>Very bad (Bardzo zły).</summary>
    VeryBad = 5
}
