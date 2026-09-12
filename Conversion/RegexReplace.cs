using CRMPark.FetchOperations.Base;
using CRMPark.FetchOperations.Search;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using static CRMPark.FetchOperations.Fetch.BindUnbindFetchXMLRecords;

namespace CRMPark.FetchOperations.Conversion
{
    public class RegexReplace : RegexStepBase
    {
        [Input("Replacement")]
        public InArgument<string> Replacement { get; set; }

        [Output("Result String")]
        public OutArgument<string> ResultString { get; set; }

        public RegexReplace() 
        {
            _workflowName = "RegexReplace";
        }

        protected override void ExecuteActivity(WorkflowActivityContext context, IOrganizationService service, string sourceString, string regularExpression)
        {
            // Задаём выходные значения по умолчанию
            ResultString.Set(context.CodeActivityContext, string.Empty);

            // Получаем входные параметры шага
            var replacement = Replacement.Get(context.CodeActivityContext);

            // Выходим из шага при пустой входной строке и/или регулярном выражении
            if (string.IsNullOrEmpty(sourceString) || string.IsNullOrEmpty(regularExpression))
            {
                throw new InvalidPluginExecutionException($"В шаг была передана пустая входная строка и/или регулярное выражение. ");
            }

            // С помощью Regex заменяем все вхождения выражения в строке на заменитель
            var resultString = Regex.Replace(sourceString, regularExpression, replacement);

            // Сформированную строку присваиваем в выходной параметр ResultString
            ResultString.Set(context.CodeActivityContext, resultString);
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
            inputArguments.AppendLine($"Replacement: {Replacement.Get(context.CodeActivityContext)}");
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
            outputArguments.AppendLine($"Result String: {ResultString.Get(context.CodeActivityContext)}");
            _resultText.AppendLine(outputArguments.ToString());
        }
    }
}
