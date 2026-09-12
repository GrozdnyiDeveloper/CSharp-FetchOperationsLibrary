using CRMPark.FetchOperations.Base;
using CRMPark.FetchOperations.Fetch;
using CRMPark.FetchOperations.Helpers;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;

namespace CRMPark.FetchOperations.Fetch
{
    public class ConcatFetchXMLData : FetchStepBase
    {
        [RequiredArgument]
        [Input("Concat Attribute")]
        public InArgument<string> ConcatAttribute { get; set; }

        [Input("String Concatenation Separator")]
        public InArgument<string> StringConcatenationSeparator { get; set; }

        [Input("String Concatenation Length")]
        public InArgument<int> StringConcatenationLength { get; set; }

        [Output("Result")]
        public OutArgument<string> Result { get; set; }

        public ConcatFetchXMLData()
        {
            // Задаём в глобальную переменную название шага
            _workflowName = "ConcatFetchXMLData";
        }

        protected override void ExecuteActivity(WorkflowActivityContext context, IOrganizationService service, List<string> attributeNames, DataCollection<Entity> entites)
        {
            // Получаем входные параметры 
            var concatAttribute = ConcatAttribute.Get(context.CodeActivityContext);
            var stringConcatenationSeparator = StringConcatenationSeparator.Get(context.CodeActivityContext);
            var stringConcatenationLength = StringConcatenationLength.Get(context.CodeActivityContext);

            // Если в результате fetch-запроса получили записи
            if (entites.Count != 0)
            {
                // Формируем итоговую строку конкатенации 
                var (concatResult, processedCount) = ConcatValues(service, entites, concatAttribute, stringConcatenationSeparator, stringConcatenationLength);
                if (concatResult != null)
                {
                    Result.Set(context.CodeActivityContext, concatResult);
                }
            }

            _processedAmount = entites.Count;
        }

        /// <summary>
        /// Метод по конкатенации значений указанного поля из полученных записей
        /// </summary>
        /// <param name="service"></param>
        /// <param name="entites">Список полученных записей</param>
        /// <param name="attributeName">Название поля для конкатенации</param>
        /// <param name="separator">Разделитель</param>
        /// <param name="length">Ограничение длины итоговой строки</param>
        /// <returns></returns>
        /// <exception cref="InvalidPluginExecutionException"></exception>
        public (string, int) ConcatValues(IOrganizationService service, DataCollection<Entity> entites, string attributeName, string separator, int length)
        {
            // Выходим из метода если не было получено записей 
            if (entites == null || entites.Count == 0)
            {
                return (null, 0);
            }

            // Выдаём ошибку если записи не содержат указанного поля
            if (!entites.Any<Entity>(x => x.Attributes.Contains(attributeName) && x[attributeName] != null))
            {
                throw new InvalidPluginExecutionException($"В полученной последовательности данных отсутствует указанное для конкатенации поле {attributeName}");
            }

            // Получаем все значения указаннного поля
            var values = entites
                .Select(entity => GetStringValue(entity, attributeName))
                .Where(v => v != null)
                .Cast<string>()
                .ToList();

            // Если не получили значений полей, то выходим из метода
            if (values.Count == 0)
            {
                return (null, 0);
            }

            // Формируем строку конкатенации по полученным значениям из записей указанного поля с учётом разделителя 
            var concatString = string.Join(separator, values);

            // Выдаём результат с учётом ограничения по длине
            return (length == 0 ? concatString : concatString.Substring(0, length) + (concatString.Length > length ? "..." : ""), values.Count);
        }

        /// <summary>
        /// Метод получения строкового значения из CRM в зависимости от его типа
        /// </summary>
        /// <param name="entity">Запись</param>
        /// <param name="attributeName">Название поля</param>
        /// <returns>Строковое значение</returns>
        protected string GetStringValue(Entity entity, string attributeName)
        {
            var value = entity.Attributes[attributeName];

            // Для ссылки на запись получаем только её GUID 
            if (value is EntityReference entityRef)
            {
                return entityRef.Id.ToString();
            }

            // Для параметра получаем его форматированное (отображаемое) значение
            if (value is OptionSetValue option)
            {
                return entity.FormattedValues[attributeName];
            }

            // Для валюты получаем её фактическое значение
            if (value is Money money)
            {
                return money.Value.ToString();
            }

            // Для даты получает отформатированное значение
            if (value is DateTime dateTime)
            {
                return entity.FormattedValues[attributeName];
            }

            // При пустом вводе вызвращаем также пустоты
            if (value is null)
            {
                return null;
            }
            
            // В остальных случаях просто конвертируем значение в строку
            return value.ToString();
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
            inputArguments.AppendLine($"Concat Attribute: {ConcatAttribute.Get(context.CodeActivityContext)}");
            inputArguments.AppendLine($"String Concatenation Separator: {StringConcatenationSeparator.Get(context.CodeActivityContext)}");
            inputArguments.AppendLine($"String Concatenation Length: {StringConcatenationLength.Get(context.CodeActivityContext)}");
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
            outputArguments.AppendLine($"Result: {Result.Get(context.CodeActivityContext)}");
            _resultText.AppendLine(outputArguments.ToString());
        }
    }
}
