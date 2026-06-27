namespace Moe.Modules.FasPayment.Domain.Fas;

internal sealed class FasTierCriteriaNationality
{
    private FasTierCriteriaNationality() { }
    public long FasTierCriteriaId { get; private set; }
    public string Nationality { get; private set; } = string.Empty;
    public static FasTierCriteriaNationality Create(long criteriaId, string criteriaType, string value)
    {
        if (criteriaId <= 0) throw new ArgumentOutOfRangeException(nameof(criteriaId));
        string type = criteriaType?.Trim().ToUpperInvariant() ?? string.Empty;
        string normalized = value?.Trim() ?? string.Empty;
        bool supported = type switch
        {
            "NATIONALITY" or "PARENT_NATIONALITY" => FasNationalities.All.Contains(normalized, StringComparer.Ordinal),
            "ACCOUNT_TYPE" => normalized is "EDUCATION_ACCOUNT" or "PERSONAL_ACCOUNT",
            _ => false
        };
        if (!supported) throw new ArgumentException($"Unsupported value for {type}.", nameof(value));
        return new FasTierCriteriaNationality { FasTierCriteriaId = criteriaId, Nationality = normalized };
    }
}

internal static class FasNationalities
{
    public static readonly string[] All =
    [
        "Singapore Citizen", "Permanent Resident", "International Student",
        "Afghan", "Albanian", "Algerian", "American", "Andorran", "Angolan", "Antiguan or Barbudan",
        "Argentine", "Armenian", "Australian", "Austrian", "Azerbaijani", "Bahamian", "Bahraini",
        "Bangladeshi", "Barbadian", "Belarusian", "Belgian", "Belizean", "Beninese", "Bhutanese",
        "Bolivian", "Bosnian or Herzegovinian", "Botswanan", "Brazilian", "Bruneian", "Bulgarian",
        "Burkinabe", "Burundian", "Cabo Verdean", "Cambodian", "Cameroonian", "Canadian",
        "Central African", "Chadian", "Chilean", "Chinese", "Colombian", "Comorian", "Congolese",
        "Costa Rican", "Croatian", "Cuban", "Cypriot", "Czech", "Danish", "Djiboutian", "Dominican",
        "Dutch", "Ecuadorian", "Egyptian", "Emirati", "Equatorial Guinean", "Eritrean", "Estonian",
        "Eswatini", "Ethiopian", "Fijian", "Filipino", "Finnish", "French", "Gabonese", "Gambian",
        "Georgian", "German", "Ghanaian", "Greek", "Grenadian", "Guatemalan", "Guinean", "Guyanese",
        "Haitian", "Honduran", "Hungarian", "Icelandic", "Indian", "Indonesian", "Iranian", "Iraqi",
        "Irish", "Israeli", "Italian", "Ivorian", "Jamaican", "Japanese", "Jordanian", "Kazakhstani",
        "Kenyan", "Kiribati", "Kuwaiti", "Kyrgyzstani", "Laotian", "Latvian", "Lebanese", "Liberian",
        "Libyan", "Liechtensteiner", "Lithuanian", "Luxembourgish", "Malagasy", "Malawian", "Malaysian",
        "Maldivian", "Malian", "Maltese", "Marshallese", "Mauritanian", "Mauritian", "Mexican",
        "Micronesian", "Moldovan", "Monacan", "Mongolian", "Montenegrin", "Moroccan", "Mozambican",
        "Myanmar national", "Namibian", "Nauruan", "Nepalese", "New Zealander", "Nicaraguan",
        "Nigerian", "Nigerien", "North Korean", "North Macedonian", "Norwegian", "Omani", "Pakistani",
        "Palauan", "Palestinian", "Panamanian", "Papua New Guinean", "Paraguayan", "Peruvian", "Polish",
        "Portuguese", "Qatari", "Romanian", "Russian", "Rwandan", "Saint Lucian", "Salvadoran",
        "Samoan", "San Marinese", "Sao Tomean", "Saudi Arabian", "Senegalese", "Serbian", "Seychellois",
        "Sierra Leonean", "Slovak", "Slovenian", "Solomon Islander", "Somali", "South African",
        "South Korean", "South Sudanese", "Spanish", "Sri Lankan", "Sudanese", "Surinamese", "Swedish",
        "Swiss", "Syrian", "Taiwanese", "Tajikistani", "Tanzanian", "Thai", "Timorese", "Togolese",
        "Tongan", "Trinidadian or Tobagonian", "Tunisian", "Turkish", "Turkmen", "Tuvaluan", "Ugandan",
        "Ukrainian", "Uruguayan", "Uzbekistani", "Vanuatuan", "Venezuelan", "Vietnamese", "Yemeni",
        "Zambian", "Zimbabwean", "Other nationality"
    ];
}
