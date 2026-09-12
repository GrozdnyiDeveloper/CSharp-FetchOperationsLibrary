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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using static CRMPark.FetchOperations.Fetch.BindUnbindFetchXMLRecords;

namespace CRMPark.FetchOperations.Conversion
{
    public class ParseString : MainStepBase
    {
        [RequiredArgument]
        [Input("Source String")]
        public InArgument<string> SourceString { get; set; }

        [RequiredArgument]
        [Input("Conversion Type")]
        [AttributeTarget("crmpark_option", "crmpark_conversion_typecode")]
        public InArgument<OptionSetValue> Conversion { get; set; }

        [Output("Integer")]
        public OutArgument<int> IntegerValue { get; set; }

        [Output("Decimal")]
        public OutArgument<decimal> DecimalValue { get; set; }

        [Output("Logical")]
        public OutArgument<bool> LogicalValue { get; set; }

        [Output("Datetime")]
        public OutArgument<DateTime> DatetimeValue { get; set; }

        public enum ConversionType
        {
            Integer = 557180000,
            Decimal = 557180001,
            Logical = 557180002,
            Datetime = 557180003
        }

        public ParseString() 
        {
            _workflowName = "ParseString";
        }

        public string _conversionLabel { get; set; }

        protected override void BeforeExecuteActivity(WorkflowActivityContext context, IOrganizationService service)
        {
            // Получаем значение метки параметра из входного набора параметров
            var conversion = Conversion.Get(context.CodeActivityContext);
            if (conversion != null)
            {
                _conversionLabel = _helper.GetPicklistLabel(service, "crmpark_option", "crmpark_conversion_typecode", conversion.Value);
            }
        }

        protected override void ExecuteActivity(WorkflowActivityContext context, IOrganizationService service)
        {
            // Задаём выходные значения по умолчанию
            IntegerValue.Set(context.CodeActivityContext, 0);
            DecimalValue.Set(context.CodeActivityContext, 0M);
            LogicalValue.Set(context.CodeActivityContext, false);

            // Получаем входные параметры шага
            var sourceString = SourceString.Get(context.CodeActivityContext);
            var conversion = Conversion.Get(context.CodeActivityContext);

            // Выдаём соответствующее сообщение при пустой входной строке
            if (string.IsNullOrEmpty(sourceString))
            {
                throw new InvalidPluginExecutionException($"В шаг была передана пустая входная строка. ");
            }

            // В зависимости от указанного типа проводим попытку конверсии указанного значения в нужный тип
            switch (conversion.Value)
            {
                case (int)ConversionType.Integer:
                    if (int.TryParse(sourceString, NumberStyles.Any, CultureInfo.InvariantCulture, out int intResult))
                    {
                        IntegerValue.Set(context.CodeActivityContext, intResult);
                    }
                    else
                    {
                        // Если не удалась стандартная конверсия, то получаем из строки только числовую часть и повторяем попытку
                        sourceString = Regex.Match(Regex.Replace(sourceString, @"\s+", ""), @"[\d.,]+").Value.Replace(',', '.');
                        if (int.TryParse(sourceString, NumberStyles.Any, CultureInfo.InvariantCulture, out intResult))
                        {
                            IntegerValue.Set(context.CodeActivityContext, intResult);
                        }
                        else
                        {
                            throw new InvalidPluginExecutionException($"Не удалось конвертировать указанное значение {sourceString} в тип {_conversionLabel} ({conversion.Value}). ");
                        }
                    }

                    break;

                case (int)ConversionType.Decimal:
                    if (decimal.TryParse(sourceString, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal decimalResult))
                    {
                        DecimalValue.Set(context.CodeActivityContext, decimalResult);
                    }
                    else
                    {
                        // Если не удалась стандартная конверсия, то получаем из строки только числовую часть и повторяем попытку
                        sourceString = Regex.Match(Regex.Replace(sourceString, @"\s+", ""), @"[\d.,]+").Value.Replace(',', '.');
                        if (decimal.TryParse(sourceString, NumberStyles.Any, CultureInfo.InvariantCulture, out decimalResult))
                        {
                            DecimalValue.Set(context.CodeActivityContext, decimalResult);
                        }
                        else
                        {
                            throw new InvalidPluginExecutionException($"Не удалось конвертировать указанное значение {sourceString} в тип {_conversionLabel} ({conversion.Value}). ");
                        }
                    }
                    break;

                case (int)ConversionType.Logical:
                    if (bool.TryParse(sourceString, out bool boolResult))
                    {
                        LogicalValue.Set(context.CodeActivityContext, boolResult);
                    }
                    else
                    {
                        throw new InvalidPluginExecutionException($"Не удалось конвертировать указанное значение {sourceString} в тип {_conversionLabel} ({conversion.Value}). ");
                    }
                    break;

                case (int)ConversionType.Datetime:
                    if (DateTime.TryParse(sourceString, out DateTime dateResult))
                    {
                        DatetimeValue.Set(context.CodeActivityContext, dateResult);
                    }
                    else
                    {
                        throw new InvalidPluginExecutionException($"Не удалось конвертировать указанное значение {sourceString} в тип {_conversionLabel} ({conversion.Value}). ");
                    }
                    break;
            }
        }

        /// <summary>
        /// Метод указания входных значений в текст результата
        /// </summary>
        /// <param name="context">Контекст CRM</param>
        protected override void GetInputArguments(WorkflowActivityContext context)
        {
            // Формируем строку с меткой
            var conversionString = string.IsNullOrEmpty(_conversionLabel) ? $"{Conversion.Get(context.CodeActivityContext)?.Value}" : _conversionLabel + $"({Conversion.Get(context.CodeActivityContext)?.Value})";

            // Получаем наследуемую строку 
            base.GetInputArguments(context);
            StringBuilder inputArguments = new StringBuilder();
            inputArguments.AppendLine($"Source String: {SourceString.Get(context.CodeActivityContext)}");
            inputArguments.AppendLine($"Conversion Type: {conversionString}");
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
            outputArguments.AppendLine($"Integer: {IntegerValue.Get(context.CodeActivityContext)}");
            outputArguments.AppendLine($"Decimal: {DecimalValue.Get(context.CodeActivityContext)}");
            outputArguments.AppendLine($"Logical: {LogicalValue.Get(context.CodeActivityContext)}");
            outputArguments.AppendLine($"Datetime: {DatetimeValue.Get(context.CodeActivityContext)}");
            _resultText.AppendLine(outputArguments.ToString());
        }
    }
}
