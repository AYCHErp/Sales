﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Frapid.Configuration;
using Frapid.Configuration.Db;
using Frapid.DataAccess;
using Frapid.Framework.Extensions;
using Frapid.Mapper.Database;
using Frapid.Mapper.Query.Insert;
using MixERP.Sales.DTO;
using MixERP.Sales.QueryModels;
using MixERP.Sales.ViewModels;

namespace MixERP.Sales.DAL.Backend.Tasks
{
    public static class Quotations
    {
        public static async Task<long> PostAsync(string tenant, Quotation model)
        {
            using (var db = DbProvider.Get(FrapidDbServer.GetConnectionString(tenant), tenant).GetDatabase())
            {
                var awaiter = await db.InsertAsync("sales.quotations", "quotation_id", true, model).ConfigureAwait(false);
                long quotationId = awaiter.To<long>();

                foreach (var detail in model.Details)
                {
                    detail.QuotationId = quotationId;
                    await db.InsertAsync("sales.quotation_details", "quotation_detail_id", true, detail).ConfigureAwait(false);
                }

                return quotationId;
            }
        }

        public static async Task<QuotationMergeViewModel> GetMergeModelAsync(string tenant, long quotationId)
        {
            string sql = "SELECT *, inventory.get_customer_code_by_customer_id(sales.quotations.customer_id) AS customer_name FROM sales.quotations WHERE quotation_id=@0;";
            var quotation = await Factory.GetAsync<QuotationInfo>(tenant, sql, quotationId).ConfigureAwait(false);

            sql = "SELECT * FROM sales.quotation_details WHERE quotation_id=@0;";
            var details = await Factory.GetAsync<QuotationDetail>(tenant, sql, quotationId).ConfigureAwait(false);

            return new QuotationMergeViewModel
            {
                Quotation = quotation.FirstOrDefault(),
                Details = details
            };
        }

        public static async Task<List<QuotationResultview>> GetQuotationResultViewAsync(string tenant, QuotationQueryModel query)
        {
            string sql = "SELECT * FROM sales.get_quotation_view(@0::integer,@1::integer,@2,@3::date,@4::date,@5::date,@6::date,@7::bigint,@8,@9,@10,@11,@12);";

            if (DbProvider.GetDbType(DbProvider.GetProviderName(tenant)) == DatabaseType.SqlServer)
            {
                sql = "SELECT * FROM sales.get_quotation_view(@0,@1,@2,@3,@4,@5,@6,@7,@8,@9,@10,@11,@12);";
            }

            var awaiter = await
                Factory.GetAsync<QuotationResultview>(tenant, sql, query.UserId, query.OfficeId, query.Customer.Or(""), query.From, query.To,
                    query.ExpectedFrom, query.ExpectedTo, query.Id, query.ReferenceNumber.Or(""),
                    query.InternalMemo.Or(""), query.Terms.Or(""), query.PostedBy.Or(""), query.Office.Or("")).ConfigureAwait(false);

            return awaiter.OrderBy(x => x.ValueDate).ThenBy(x => x.Supplier).ToList();
        }
    }
}