using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moe.StudentFinance.Persistence;

#nullable disable

namespace Moe.StudentFinance.Migrations.Migrations
{
    [DbContext(typeof(MoeDbContext))]
    [Migration("20260706101000_AddTopUpCampaignRepeatDayFields")]
    partial class AddTopUpCampaignRepeatDayFields
    {
    }
}
