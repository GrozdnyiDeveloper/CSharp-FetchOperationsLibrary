using CRMPark.FetchOperations.Base;
using CRMPark.FetchOperations.Conversion;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using Microsoft.Xrm.Sdk.Workflow.Activities;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using static CRMPark.FetchOperations.Fetch.BindUnbindFetchXMLRecords;

namespace CRMPark.FetchOperations.Common
{
    public class MathOperation : MainStepBase
    {
        [Input("Setting Key")]
        public InArgument<string> SettingKey { get; set; }

        [Input("Expression")]
        public InArgument<string> Expression { get; set; }

        [Input("Arguments ")]
        public InArgument<string> Arguments { get; set; }

        [Output("Result")]
        public OutArgument<decimal> Result { get; set; }

        public string _formatedExpression { get; set; }

        public MathOperation() 
        {
            _workflowName = "MathOperation";
        }

        protected override void ExecuteActivity(WorkflowActivityContext context, IOrganizationService service)
        {
            // Устанавливаем выходные параметры на значения по умолчанию
            Result.Set(context.CodeActivityContext, 0);

            // Получаем входные параметры 
            var settingKey = SettingKey.Get(context.CodeActivityContext);
            var expression = Expression.Get(context.CodeActivityContext);
            var arguments = Arguments.Get(context.CodeActivityContext);

            // Проверяем заполнение обязательных параметров
            if (string.IsNullOrEmpty(settingKey) && string.IsNullOrEmpty(expression))
            {
                throw new InvalidPluginExecutionException($"Для работы шага требуется указать либо ключ параметра (Setting Key), либо выражение (Expression). ");
            }

            // Если указан ключ параметра
            if (!string.IsNullOrEmpty(settingKey))
            {
                // Производим попытку получения записи Параметра по указанному ключу
                var setting = _helper.GetSettingByKey(service, settingKey);

                // Проверяем тип полученного параметра
                var optionTypeCode = setting?.GetAttributeValue<OptionSetValue>("crmpark_option_typecode")?.Value;
                if (optionTypeCode == null || optionTypeCode != (int)OptionType.Math)
                {
                    throw new InvalidPluginExecutionException($"Была получена запись параметра с типом не математического выражения. ");
                }

                // Получаем значение математического выражения из параметра
                expression = setting?.GetAttributeValue<string>("crmpark_text_value");
                if (string.IsNullOrEmpty(expression))
                {
                    throw new InvalidPluginExecutionException($"Была получена запись параметра без заданного выражения. ");
                }
            }

            // Если были переданы аргументы
            if (!string.IsNullOrEmpty(arguments))
            {
                // Получаем сформированный список аргументов
                var formatedArguments = ParseArguments(arguments);

                // Подстановка корретных значений аргументов выражение
                _formatedExpression = PrepareExpression(expression, formatedArguments);
            }
            else
            {
                // Иначе считаем исходное выражение
                _formatedExpression = expression;
            }

            // Производим вычисление итогового выражения
            var result = CalculateExpression(_formatedExpression);

            // Фиксируем результат в выходной параметр
            Result.Set(context.CodeActivityContext, result);
        }

        /// <summary>
        /// Метод обработки строки аргументов
        /// </summary>
        /// <param name="arguments">Входная строка аргументов</param>
        protected Dictionary<string, string> ParseArguments(string arguments)
        {
            return arguments
                .Split("||".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split("=".ToCharArray(), 2))
                .Where(split => split.Length == 2)
                .ToDictionary(split => split[0].Trim(), split => split[1].Trim());
        }

        /// <summary>
        /// Подстановка значений аргументов в выражение
        /// </summary>
        /// <param name="expression">Изначальное выражение</param>
        /// <param name="arguments">Список аргументов и их значений</param>
        protected string PrepareExpression(string expression, Dictionary<string, string> arguments)
        {
            foreach (var argument in arguments)
            {
                var numericValue = ExtractNumericValue(argument.Value);
                expression = expression.Replace(argument.Key, numericValue);
            }

            return expression;
        }

        /// <summary>
        /// Метод получения корректного числового значения 
        /// </summary>
        /// <param name="input">Исходное строковое значение</param>
        protected string ExtractNumericValue(string input)
        {
            input = Regex.Match(Regex.Replace(input, @"\s+", ""), @"[\d.,]+").Value.Replace(',', '.');
            if (decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal decimalResult))
            {
                return decimalResult.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                return "0";
            }
        }

        /// <summary>
        /// Метод проведение вычисления заданного выражения
        /// </summary>
        /// <param name="expression">Итоговое выражение</param>
        protected decimal CalculateExpression(string expression)
        {
            var dataTable = new DataTable();
            var column = new DataColumn("Result", typeof(double), expression);
            dataTable.Columns.Add(column);
            dataTable.Rows.Add(0);

            var result = (double)dataTable.Rows[0]["Result"];

            if (double.IsNaN(result))
            {
                throw new InvalidPluginExecutionException($"Был получен некорректный результат вычисления. ");
            }

            if (double.IsPositiveInfinity(result) || double.IsNegativeInfinity(result))
            {
                throw new InvalidPluginExecutionException($"Результатом вычисления получилась бесконечность. Возможно произошло деление на ноль. ");
            }

            return (decimal)result;
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
            inputArguments.AppendLine($"Setting Key: {SettingKey.Get(context.CodeActivityContext)}");
            inputArguments.AppendLine($"Expression: {Expression.Get(context.CodeActivityContext)}");
            inputArguments.AppendLine($"Arguments: {Arguments.Get(context.CodeActivityContext)}");
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
            outputArguments.AppendLine($"Formated Expression: {_formatedExpression}");
            outputArguments.AppendLine($"Result: {Result.Get(context.CodeActivityContext)}");
            _resultText.AppendLine(outputArguments.ToString());
        }
    }
}
