﻿using OptimaJet.DWKit.Core;
using OptimaJet.DWKit.Core.Model;
using OptimaJet.HRM.Model;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using OptimaJet.HRM.Reports;

namespace OptimaJet.HRM
{
    public class DashboardInfo
    {
        //For HR Department
        public object EmployeeReport;

        public long BusinessTripCount;
        public long SickLeaveCount;
        public long? VacationCount;
        public long? CompensationCount;
        public long? RecruitmentCount;

        //For Users
        public List<Dictionary<string, object>> UserBusinessTrips;
        public decimal UserCompensationAmount;
        public decimal UserCompensationAmountApproved;
        public decimal UserBusinessTripExpenses;
        public decimal UserBusinessTripExpensesApproved;

    }
    public class Dashboard
    {
        public async static Task<DashboardInfo> Generate()
        {
            var res = new DashboardInfo();
            var employeeModel = (await MetadataToModelConverter.GetEntityModelByModelAsync("Employee"));
            var businessTripModel = (await MetadataToModelConverter.GetEntityModelByModelAsync(DocumentTypes.BusinessTrip));
            var CompensationModel = (await MetadataToModelConverter.GetEntityModelByModelAsync(DocumentTypes.Compensation));
            
            if(DWKitRuntime.Security.CheckPermission("Dashboard", "ViewCompanyInfo"))
            {
                Filter notDraft = Filter.And.NotIn(WorkflowReport.DraftStates, "State");
                res.BusinessTripCount = await Document.GetDocumentTypeCountAsync(DocumentTypes.BusinessTrip, notDraft);
                res.SickLeaveCount = await Document.GetDocumentTypeCountAsync(DocumentTypes.SickLeave, notDraft);
                res.VacationCount = await Document.GetDocumentTypeCountAsync(DocumentTypes.Vacation, notDraft);
                res.CompensationCount = await Document.GetDocumentTypeCountAsync(DocumentTypes.Compensation, notDraft);
                res.RecruitmentCount = await Document.GetDocumentTypeCountAsync(DocumentTypes.Recruitment, notDraft);

                res.EmployeeReport = await EmployeeReport.GenerateForDashboard(DateTime.Now.AddMonths(-5), DateTime.Now);
            }

            if (DWKitRuntime.Security.CurrentUser != null)
            {
                #region For Users
                Guid id = DWKitRuntime.Security.CurrentUser.Id;
                Filter userBusinessTripFilter = DocumentTypes.GetFilter(DocumentTypes.BusinessTrip).NotIn(WorkflowReport.DraftStates, "State").LikeRightLeft(id.ToString(), "Employees");
                var trips = await businessTripModel.GetAsync(userBusinessTripFilter, Order.StartAsc("DateStart"));

                Filter userCompenstationFilter = DocumentTypes.GetFilter(DocumentTypes.Compensation).NotIn(WorkflowReport.DraftStates, "State").Equal(id, "EmployeeId");
                var Compensations = await CompensationModel.GetAsync(userCompenstationFilter);
                
                res.UserBusinessTripExpenses = 0;
                res.UserBusinessTripExpensesApproved = 0;
                foreach(dynamic trip in trips)
                {
                    if (trip.Amount == null || trip.Amount == 0)
                        continue;
                    
                    res.UserBusinessTripExpenses += (decimal)trip.Amount;
                    if (WorkflowReport.FinalStates.Contains(trip.State))
                    {
                        res.UserBusinessTripExpensesApproved += (decimal)trip.Amount;
                    }
                }

                res.UserCompensationAmount = 0;
                res.UserCompensationAmountApproved = 0;
                foreach (dynamic c in Compensations)
                {
                    if (c.Amount == null || c.Amount == 0)
                        continue;

                    res.UserCompensationAmount += (decimal)c.Amount;
                    if (WorkflowReport.FinalStates.Contains(c.State))
                    {
                        res.UserCompensationAmountApproved += (decimal)c.Amount;
                    }
                }

                res.UserBusinessTrips = trips.Where(c => ((dynamic)c).DateEnd > DateTime.Now).Select(c=> c.ToDictionary(true)).ToList();
                #endregion
            }

            return res;
        }

        public async static Task<object> GetMenuData()
        {
            var userId = DWKitRuntime.Security.CurrentUser.ImpersonatedUserId.HasValue ? DWKitRuntime.Security.CurrentUser.ImpersonatedUserId.Value :
                    DWKitRuntime.Security.CurrentUser.Id;
            var inboxModel = await MetadataToModelConverter.GetEntityModelByModelAsync("WorkflowInbox");
            var historyModel = await MetadataToModelConverter.GetEntityModelByModelAsync("WorkflowProcessTransitionHistory");

            var tripFilter = await Document.GetViewFilterForCurrentUser(await MetadataToModelConverter.GetEntityModelByModelAsync(DocumentTypes.BusinessTrip));
            var sickLeaveFilter = await Document.GetViewFilterForCurrentUser(await MetadataToModelConverter.GetEntityModelByModelAsync(DocumentTypes.SickLeave));
            var vacationFilter = await Document.GetViewFilterForCurrentUser(await MetadataToModelConverter.GetEntityModelByModelAsync(DocumentTypes.Vacation));
            var compenstationFilter = await Document.GetViewFilterForCurrentUser(await MetadataToModelConverter.GetEntityModelByModelAsync(DocumentTypes.Compensation));
            var recruitmentFilter = await Document.GetViewFilterForCurrentUser(await MetadataToModelConverter.GetEntityModelByModelAsync(DocumentTypes.Recruitment));
            
            return new
            {
                inbox = await inboxModel.GetCountAsync(Filter.And.Equal(userId, "IdentityId")),
                outbox = await historyModel.GetCountAsync(Filter.And.Equal(userId.ToString(), "ExecutorIdentityId")),
                trip = await Document.GetDocumentTypeCountAsync(DocumentTypes.BusinessTrip, tripFilter),
                sickleave = await Document.GetDocumentTypeCountAsync(DocumentTypes.SickLeave, sickLeaveFilter),
                vacation = await Document.GetDocumentTypeCountAsync(DocumentTypes.Vacation, vacationFilter),
                compenstation = await Document.GetDocumentTypeCountAsync(DocumentTypes.Compensation, compenstationFilter),
                recruitment = await Document.GetDocumentTypeCountAsync(DocumentTypes.Recruitment, recruitmentFilter),
                employeeform = DWKitRuntime.Security.CheckPermission("Employee", "View"),
                documentsform = DWKitRuntime.Security.CheckPermission("Documents", "View"),
                workflowreport = DWKitRuntime.Security.CheckPermission("WorkflowReport", "View"),
                employeereport = DWKitRuntime.Security.CheckPermission("EmployeeReport", "View"),
                workcalendar = DWKitRuntime.Security.CheckPermission("WorkCalendar", "View")
            };            
        }
    }
}
