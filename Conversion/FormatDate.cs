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
    public class FormatDate : MainStepBase
    {
        [RequiredArgument]
        [Input("Date")]
        public InArgument<DateTime> Date { get; set; }

        [RequiredArgument]
        [Input("Convert to UTC")]
        public InArgument<bool> ConvertToUTC { get; set; }

        [Input("Format")]
        public InArgument<string> Format { get; set; }

        [Input("Culture Info")]
        public InArgument<string> CultureInfo { get; set; }

        [Output("Formated Date")]
        public OutArgument<string> FormatedDate { get; set; }

        public FormatDate() 
        {
            _workflowName = "FormatDate";
        }

        protected override void ExecuteActivity(WorkflowActivityContext context, IOrganizationService service)
        {
            // Получаем входные параметры шага
            var date = Date.Get(context.CodeActivityContext);
            var convertToUTC = ConvertToUTC.Get(context.CodeActivityContext);
            var format = Format.Get(context.CodeActivityContext);
            var сultureInfo = CultureInfo.Get(context.CodeActivityContext);

            // Конвертируем в UTC формат если указано
            date = convertToUTC ? date.ToUniversalTime() : date;

            // Формируем значения для выходных параметров
            if (!string.IsNullOrEmpty(сultureInfo))
            {
                FormatedDate.Set(context.CodeActivityContext, date.ToString(format, new CultureInfo(сultureInfo)));
            }
            else
            {
                FormatedDate.Set(context.CodeActivityContext, date.ToString(format));
            }
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
            inputArguments.AppendLine($"Format: {Format.Get(context.CodeActivityContext)}");
            inputArguments.AppendLine($"Culture Info: {CultureInfo.Get(context.CodeActivityContext)}");
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
            outputArguments.AppendLine($"Formated Date: {FormatedDate.Get(context.CodeActivityContext)}");
            _resultText.AppendLine(outputArguments.ToString());
        }
    }
}
