using CRMPark.FetchOperations.Base;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using static CRMPark.FetchOperations.Fetch.BindUnbindFetchXMLRecords;

namespace CRMPark.FetchOperations.Conversion
{
    public class DecomposeDate : MainStepBase
    {
        [RequiredArgument]
        [Input("Date")]
        public InArgument<DateTime> Date { get; set; }

        [RequiredArgument]
        [Input("Convert to UTC")]
        public InArgument<bool> ConvertToUTC { get; set; }

        [Output("Day")]
        public OutArgument<string> Day { get; set; }

        [Output("Week Day")]
        public OutArgument<string> WeekDay { get; set; }

        [Output("Week Day Name")]
        public OutArgument<string> WeekDayName { get; set; }

        [Output("Month")]
        public OutArgument<string> Month { get; set; }

        [Output("Month Name")]
        public OutArgument<string> MonthName { get; set; }

        [Output("Year(YYYY)")]
        public OutArgument<string> YearYYYY { get; set; }

        [Output("Year(YY)")]
        public OutArgument<string> YearYY { get; set; }

        [Output("Hours")]
        public OutArgument<string> Hours { get; set; }

        [Output("Minutes")]
        public OutArgument<string> Minutes { get; set; }

        [Output("Date to String")]
        public OutArgument<string> DateToString { get; set; }

        [Output("Time to String")]
        public OutArgument<string> TimeToString { get; set; }

        public DecomposeDate() 
        {
            _workflowName = "DecomposeDate";
        }

        protected override void ExecuteActivity(WorkflowActivityContext context, IOrganizationService service)
        {
            // Получаем входные параметры шага
            var date = Date.Get(context.CodeActivityContext);
            var convertToUTC = ConvertToUTC.Get(context.CodeActivityContext);

            // Конвертируем в UTC формат если указано
            date = convertToUTC ? date.ToUniversalTime() : date;

            // Формируем значения для выходных параметров
            var ruCulture = new CultureInfo("ru-RU");
            Day.Set(context.CodeActivityContext, date.Day.ToString());
            WeekDay.Set(context.CodeActivityContext, (int)date.DayOfWeek == 0 ? "7" : ((int)date.DayOfWeek).ToString());
            WeekDayName.Set(context.CodeActivityContext, date.ToString("dddd", ruCulture));
            Month.Set(context.CodeActivityContext, date.Month.ToString());
            MonthName.Set(context.CodeActivityContext, date.ToString("MMMM", ruCulture));
            YearYYYY.Set(context.CodeActivityContext, date.ToString("yyyy"));
            YearYY.Set(context.CodeActivityContext, date.ToString("yy"));
            Hours.Set(context.CodeActivityContext, date.Hour.ToString());
            Minutes.Set(context.CodeActivityContext, date.Minute.ToString());
            DateToString.Set(context.CodeActivityContext, date.ToString("dd.MM.yyyy"));
            TimeToString.Set(context.CodeActivityContext, date.ToShortTimeString());
        }

        /// <summary>
        /// Метод указания входных значений в текст результата
        /// </summary>
        /// <param name="context">Контекст CRM</param>
        protected override void GetInputArguments(WorkflowActivityContext context)
        {
            // Получаем наследуемую строку 
            base.GetInputArguments(context);
            StringBuilder inputArguments = new StringBuilder();
            inputArguments.AppendLine($"Date: {Date.Get(context.CodeActivityContext)}");
            inputArguments.AppendLine($"Convert to UTC: {ConvertToUTC.Get(context.CodeActivityContext)}");
            _resultText.AppendLine(inputArguments.ToString());
        }

        /// <summary>
        /// Метод указания выходных значений в текст результата
        /// </summary>
        /// <param name="context">Контекст CRM</param>
        protected override void GetOutputArguments(WorkflowActivityContext context)
        {
            // Получаем наследуемую строку 
            base.GetOutputArguments(context);
            StringBuilder outputArguments = new StringBuilder();
            outputArguments.AppendLine($"Day: {Day.Get(context.CodeActivityContext)}");
            outputArguments.AppendLine($"Week Day: {WeekDay.Get(context.CodeActivityContext)}");
            outputArguments.AppendLine($"Week Day Name: {WeekDayName.Get(context.CodeActivityContext)}");
            outputArguments.AppendLine($"Month: {Month.Get(context.CodeActivityContext)}");
            outputArguments.AppendLine($"Month Name: {MonthName.Get(context.CodeActivityContext)}");
            outputArguments.AppendLine($"Year(YYYY): {YearYYYY.Get(context.CodeActivityContext)}");
            outputArguments.AppendLine($"Year(YY): {YearYY.Get(context.CodeActivityContext)}");
            outputArguments.AppendLine($"Hours: {Hours.Get(context.CodeActivityContext)}");
            outputArguments.AppendLine($"Minutes: {Minutes.Get(context.CodeActivityContext)}");
            outputArguments.AppendLine($"Date to String: {DateToString.Get(context.CodeActivityContext)}");
            outputArguments.AppendLine($"Time to String: {TimeToString.Get(context.CodeActivityContext)}");
            _resultText.AppendLine(outputArguments.ToString());
        }
    }
}
