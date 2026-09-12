using CRMPark.FetchOperations.Base;
using CRMPark.FetchOperations.Fetch;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CRMPark.FetchOperations.Fetch
{
    public class RollupFetchXMLData : FetchStepBase
    {
        [RequiredArgument]
        [Input("Rollup Attribute")]
        public InArgument<string> RollupAttribute { get; set; }

        [RequiredArgument]
        [Input("Operation")]
        [AttributeTarget("crmpark_option", "crmpark_operation_typecode")]
        public InArgument<OptionSetValue> Operation { get; set; }

        [Output("Result Number")]
        public OutArgument<Decimal> ResultNumber { get; set; }

        [Output("Result Date")]
        public OutArgument<DateTime> ResultDate { get; set; }

        public enum AggregateType
        {
            Count = 557180000,
            CountUnique = 557180001,
            Sum = 557180002,
            Average = 557180003,
            Max = 557180004,
            Min = 557180005
        }

        public RollupFetchXMLData()
        {
            // Задаём в глобальную переменную название шага
            _workflowName = "RollupFetchXMLData";
        }

        public string _operationLabel { get; set; }

        protected override void BeforeExecuteActivity(WorkflowActivityContext context, IOrganizationService service)
        {
            // Получаем значение метки параметра из входного набора параметров
            var operation = Operation.Get(context.CodeActivityContext);
            if (operation != null)
            {
                _operationLabel = _helper.GetPicklistLabel(service, "crmpark_option", "crmpark_operation_typecode", operation.Value);
            }
        }

        protected override void ExecuteActivity(WorkflowActivityContext context, IOrganizationService service, List<string> attributeNames, DataCollection<Entity> entites)
        {
            // Получаем входные параметры 
            var rollupAttribute = RollupAttribute.Get(context.CodeActivityContext);
            var operationCode = Operation.Get(context.CodeActivityContext).Value;

            // Если в результате fetch-запроса получили записи
            if (entites.Count != 0)
            {
                // Производим агрегацию значений согласно заданным параметрам
                var resultValue = AggregateValues(entites, rollupAttribute, operationCode);

                // Если результат агрегации не пустой
                if (resultValue != null)
                {
                    // Присваиваем результат в соответсвующий выходной параметр
                    if (resultValue is DateTime)
                    {
                        ResultDate.Set(context.CodeActivityContext, resultValue);
                    }
                    else
                    {
                        ResultNumber.Set(context.CodeActivityContext, resultValue);
                    }
                }
            }
        }

        /// <summary>
        /// Метод агрегации переданных значений огласно выбранному типу
        /// </summary>
        /// <param name="entites">Полученные записи</param>
        /// <param name="attributeName">Поле для агрегации</param>
        /// <param name="type">Тип проводимой агрегации</param>
        public dynamic AggregateValues(DataCollection<Entity> entites, string attributeName, int type)
        {
            // Выходим из метода если не было получено записей 
            if (entites == null || entites.Count == 0)
            {
                return null;
            }

            // Выдаём ошибку если записи не содержат указанного поля
            if (!entites.Any<Entity>(x => x.Attributes.Contains(attributeName) && x[attributeName] != null))
            {
                throw new InvalidPluginExecutionException($"В полученной последовательности данных отсутствует указанное для свертки поле {attributeName}");
            }

            // Получаем значения с проверкой типа
            var valuesWithTypes = entites
                .Where(r => r.Attributes.ContainsKey(attributeName) && r[attributeName] != null)
                .Select(r => GetFormatedValue(r[attributeName]))
                .Where(v => v != null || (v is DateTime && (DateTime)v != DateTime.MinValue))
                .ToList();

            _processedAmount = valuesWithTypes.Count;

            // Если не получили значений полей, то выходим из метода
            if (valuesWithTypes.Count == 0)
            {
                return null;
            }

            // Определяем тип первого значения для проверки совместимости
            Type valueType = valuesWithTypes[0].GetType();

            // В зависимости от выбранного типа свертки
            switch (type)
            {
                case (int)AggregateType.Count:
                    // Производим счёт записей
                    return valuesWithTypes.Count;

                case (int)AggregateType.CountUnique:
                    // Производим счёт уникальных записей
                    return valuesWithTypes.Distinct().Count();

                case (int)AggregateType.Sum:
                case (int)AggregateType.Average:
                    // Проверка на числовые операции
                    if (!_helper.IsNumericType(valueType))
                    {
                        throw new InvalidPluginExecutionException($"Тип {valueType.Name} не поддерживает операции суммы/среднего");
                    }    

                    // Получаем все значения указанного поля из записей
                    var numericValues = valuesWithTypes.Select(v => Convert.ToDecimal(v)).Cast<decimal>().ToList();

                    // В зависимости от типа свёртки производим суммирование значений или нахождение среднего
                    if (type == (int)AggregateType.Sum)
                        return numericValues.Sum();
                    else
                        return numericValues.Average();

                case (int)AggregateType.Max:
                    // Возвращаем максимальное значение даты или числа в зависимости от типа поля, иначе выдаём соотвествующее сообщение об ошибке
                    if (valueType == typeof(DateTime))
                        return valuesWithTypes.Cast<DateTime>().Max();
                    else if (_helper.IsNumericType(valueType))
                        return valuesWithTypes.Select(v => Convert.ToDecimal(v)).Max();
                    else
                        throw new InvalidOperationException($"Тип {valueType.Name} не поддерживает операцию поиска максимального значения");

                case (int)AggregateType.Min:
                    // Возвращаем минимальное значение даты или числа в зависимости от типа поля, иначе выдаём соотвествующее сообщение об ошибке
                    if (valueType == typeof(DateTime))
                        return valuesWithTypes.Cast<DateTime>().Min();
                    else if (_helper.IsNumericType(valueType))
                        return valuesWithTypes.Select(v => Convert.ToDecimal(v)).Min();
                    else
                        throw new InvalidOperationException($"Тип {valueType.Name} не поддерживает операцию поиска минимального значения");

                default:
                    // В остальных случаях указываем, что тип агрегации не поддерживается
                    throw new InvalidOperationException($"Тип агрегации {type} не поддерживается");
            }
        }

        /// <summary>
        /// Метод получения сформатированного значения из CRM в зависимости от его типа
        /// </summary>
        /// <param name="value">Исходное значение</param>
        protected dynamic GetFormatedValue(dynamic value)
        {
            // Для ссылки на запись получаем только её GUID 
            if (value is EntityReference entityRef)
            {
                return entityRef.Id.ToString();
            }

            // Для параметра получаем его фактическое значение
            if (value is OptionSetValue option)
            {
                return option.Value;
            }

            // Для валюты получаем её фактическое значение
            if (value is Money money)
            {
                return money.Value;
            }

            // При пустом вводе вызвращаем также пустоты
            if (value is null)
            {
                return null;
            }

            // В остальных случаях просто возвращаем исходное значение
            return value;
        }

        /// <summary>
        /// Метод указания входных значений в текст результата
        /// </summary>
        /// <param name="context">Контекст CRM</param>
        protected override void GetInputArguments(WorkflowActivityContext context)
        {
            // Формируем строку с меткой
            var operationString = string.IsNullOrEmpty(_operationLabel) ? $"{Operation.Get(context.CodeActivityContext)?.Value}" : _operationLabel + $"({Operation.Get(context.CodeActivityContext)?.Value})";

            // Получаем наследуемую строку 
            base.GetInputArguments(context);
            StringBuilder inputArguments = new StringBuilder();
            inputArguments.AppendLine($"Rollup Attribute: {RollupAttribute.Get(context.CodeActivityContext)}");
            inputArguments.AppendLine($"Operation: {operationString}");
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
            outputArguments.AppendLine($"Result Number: {ResultNumber.Get(context.CodeActivityContext)}");
            outputArguments.AppendLine($"Result Date: {ResultDate.Get(context.CodeActivityContext)}");
            _resultText.AppendLine(outputArguments.ToString());
        }
    }
}
