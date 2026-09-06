using PaycheckCalculator.Core.Models;

namespace PaycheckCalculator.Core.Tax.State;

/// <summary>
/// Full display names for <see cref="UsState"/>. Lives in Core so the results
/// pages, exporters and both front-ends print the same heading — the map used to
/// be duplicated in MAUI's <c>EnumDisplay</c> and Blazor's <c>StateMetadata</c>.
/// </summary>
public static class UsStateNames
{
    /// <summary>Returns the jurisdiction's full name, e.g. <c>UsState.MD</c> → "Maryland".</summary>
    public static string GetDisplayName(UsState state) => state switch
    {
        UsState.AL => "Alabama",
        UsState.AK => "Alaska",
        UsState.AZ => "Arizona",
        UsState.AR => "Arkansas",
        UsState.CA => "California",
        UsState.CO => "Colorado",
        UsState.CT => "Connecticut",
        UsState.DC => "District of Columbia",
        UsState.DE => "Delaware",
        UsState.FL => "Florida",
        UsState.GA => "Georgia",
        UsState.HI => "Hawaii",
        UsState.ID => "Idaho",
        UsState.IL => "Illinois",
        UsState.IN => "Indiana",
        UsState.IA => "Iowa",
        UsState.KS => "Kansas",
        UsState.KY => "Kentucky",
        UsState.LA => "Louisiana",
        UsState.ME => "Maine",
        UsState.MD => "Maryland",
        UsState.MA => "Massachusetts",
        UsState.MI => "Michigan",
        UsState.MN => "Minnesota",
        UsState.MS => "Mississippi",
        UsState.MO => "Missouri",
        UsState.MT => "Montana",
        UsState.NE => "Nebraska",
        UsState.NV => "Nevada",
        UsState.NH => "New Hampshire",
        UsState.NJ => "New Jersey",
        UsState.NM => "New Mexico",
        UsState.NY => "New York",
        UsState.NC => "North Carolina",
        UsState.ND => "North Dakota",
        UsState.OH => "Ohio",
        UsState.OK => "Oklahoma",
        UsState.OR => "Oregon",
        UsState.PA => "Pennsylvania",
        UsState.RI => "Rhode Island",
        UsState.SC => "South Carolina",
        UsState.SD => "South Dakota",
        UsState.TN => "Tennessee",
        UsState.TX => "Texas",
        UsState.UT => "Utah",
        UsState.VT => "Vermont",
        UsState.VA => "Virginia",
        UsState.WA => "Washington",
        UsState.WV => "West Virginia",
        UsState.WI => "Wisconsin",
        UsState.WY => "Wyoming",
        _ => state.ToString()
    };
}
