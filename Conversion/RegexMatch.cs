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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using static CRMPark.FetchOperations.Fetch.BindUnbindFetchXMLRecords;

namespace CRMPark.FetchOperations.Conversion
{
    public class RegexMatch : RegexStepBase
    {
        [Input("Separator")]
        public InArgument<string> Separator { get; set; }

        [Output("First Occurrence")]
        public OutArgument<string> FirstOccurrence { get; set; }

        [Output("All Occurrences")]
        public OutArgument<string> AllOccurrences { get; set; }

        public RegexMatch() 
        {
            _workflowName = "RegexMatch";
        }

        protected override void ExecuteActivity(WorkflowActivityContext context, IOrganizationService service, string sourceString, string regularExpression)
        {
            // Задаём выходные значения по умолчанию
            FirstOccurrence.Set(context.CodeActivityContext, string.Empty);
            AllOccurrences.Set(context.CodeActivityContext, string.Empty);

            // Получаем входные параметры шага
            var separator = Separator.Get(context.CodeActivityContext);

            // Выходим из шага при пустой входной строке и/или регулярном выражении
            if (string.IsNullOrEmpty(sourceString) || string.IsNullOrEmpty(regularExpression))
            {
                throw new InvalidPluginExecutionException($"В шаг была передана пустая входная строка и/или регулярное выражение. ");
            }

            // Ищем вхождения выражения в переданной строке
            var source = Regex.Matches(sourceString, regularExpression);

            // Если не нашли вхождений выдаём ошибку
            if (source.Count <= 0)
            {
                throw new InvalidPluginExecutionException($"В указанной строке {sourceString} не удалось найти вхождений {regularExpression}. ");
            }

            var allOccurrences = "";

            // Для всех найденных вхождений
            List<Match> list = source.Cast<Match>().Where(e => e.Success && !string.IsNullOrEmpty(e.Value)).ToList();
            for (int index = 0; index < list.Count; ++index)
            {
                Match match = list[index];

                // Самое первое вхождение присваиваем в выходной параметр FirstOccurrence
                if (index == 0)
                    FirstOccurrence.Set(context.CodeActivityContext, match.Value);

                // Формируем строку с остальными вхождениями через указанный разделитель
                separator = index == source.Count - 1 ? string.Empty : separator ?? string.Empty;
                allOccurrences = allOccurrences + match.Value + separator;
            }

            // Сформированную строку всех вхождений присваиваем в выходной параметр AllOccurrences
            AllOccurrences.Set(context.CodeActivityContext, allOccurrences);
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
            inputArguments.AppendLine($"Separator: {Separator.Get(context.CodeActivityContext)}");
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
            outputArguments.AppendLine($"First Occurrence: {FirstOccurrence.Get(context.CodeActivityContext)}");
            outputArguments.AppendLine($"All Occurrences: {AllOccurrences.Get(context.CodeActivityContext)}");
            _resultText.AppendLine(outputArguments.ToString());
        }
    }
}
