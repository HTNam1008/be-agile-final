using System;
using System.Collections.Generic;
using System.Text;

using System.Text.Json.Serialization;

namespace Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TopUpAccountSelectionMode
{
    ExplicitIds = 1,
    AllMatchingFilter = 2
}
