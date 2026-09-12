using CRMPark.FetchOperations.Base;
using CRMPark.FetchOperations.Fetch;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Workflow;
using Microsoft.Xrm.Sdk.Workflow.Activities;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.UI.WebControls.WebParts;
using System.Windows;
using System.Windows.Controls;

namespace CRMPark.FetchOperations.Fetch
{
    public class UpdateFetchXMLRecords : FetchStepBase
    {
        [RequiredArgument]
        [Input("Values to Set")]
        public InArgument<string> ValuesToSet { get; set; }

        [Input("Amount Records To Process")]
        public InArgument<int> AmountRecordsToProcess { get; set; }

        [Output("Processed Records Quantity")]
        public OutArgument<int> ProcessedRecordsQuantity { get; set; }

        public UpdateFetchXMLRecords()
        {
            // Задаём в глобальную переменную название шага
            _workflowName = "UpdateFetchXMLRecords";
        }

        protected override void ExecuteActivity(WorkflowActivityContext context, IOrganizationService service, List<string> attributeNames, DataCollection<Entity> entites)
        {
            // Получаем входные параметры 
            var valuesToSet = ValuesToSet.Get(context.CodeActivityContext);
            var amountRecordsToProcess = AmountRecordsToProcess.Get(context.CodeActivityContext);

            // Если в результате fetch-запроса получили записи
            if (entites.Count != 0)
            {
                // Получаем указанные поля и значения для обновления в записях
                Dictionary<string, dynamic> attributesValues = new Dictionary<string, dynamic>();
                attributesValues = FormateAttributesValues(service, valuesToSet, entites[0].LogicalName);

                // Для каждой записи
                for (int i = 0; i < entites.Count && (amountRecordsToProcess == 0 || i < amountRecordsToProcess); i++)
                {
                    Entity entity = entites[i];
                    try
                    {
                        // Формируем запрос на обновление данных в записи
                        Entity updateEntity = new Entity(entity.LogicalName, entity.Id);
                        foreach (var condition in attributesValues)
                        {
                            updateEntity[condition.Key] = condition.Value;
                        }
                        service.Update(updateEntity);
                        _processedAmount++;
                    }
                    catch (Exception ex)
                    {
                        // Добавление возникших ошибок по каждой записи из запроса 
                        _externalErrors.AppendLine(string.Format("{0} - {1}", entity.Id.ToString(), ex.Message));
                    }
                }
            }

            // Фиксируем кол-во успешно обработанных записей
            ProcessedRecordsQuantity.Set(context.CodeActivityContext, _processedAmount);
        }

        /// <summary>
        /// Метод получения из переданной строки списка условий с корректными форматами значений
        /// </summary>
        /// <param name="service">Сервис CRM</param>
        /// <param name="attributes">Полученные шагом строка значений условий</param>
        /// <param name="entityName">Логическое имя сущности</param>
        /// <returns>Лист условий с корректными форматами значений</returns>
        public Dictionary<string, dynamic> FormateAttributesValues(IOrganizationService service, string attributes, string entityName)
        {
            Dictionary<string, dynamic> result = new Dictionary<string, dynamic>();

            // Выводим ошибку если не указаны данные для обновления
            if (attributes.Length == 0)
            {
                throw new InvalidPluginExecutionException($"Не указаны атрибуты для обновлений в записях");
            }

            try
            {
                // Получаем все значения условий из текста
                List<string> attributesValues = attributes.Split(new[] { "||" }, StringSplitOptions.None).ToList();
                foreach (string attribute in attributesValues)
                {
                    var parts = attribute.Split(new[] { '=' }, 2);
                    if (parts == null || parts.Count() != 2)
                    {
                        throw new InvalidPluginExecutionException($"Условие {attribute} указано в некорректном формате");
                    }

                    // Получаем их условия название поля и его значение
                    var name = parts[0];
                    var value = parts[1];
                    var type = string.Empty;

                    // Получаем указанный в параметре тип
                    var pattern = @"\[([^\]]+)\]";
                    var match = Regex.Match(value, pattern);
                    if (match.Success)
                    {
                        type = match.Value.Trim('[', ']').ToUpper();
                        value = value.Replace(match.Value, "");
                    }

                    // В зависимости от указанного типа получаем значение в корректном для CRM формате
                    dynamic formatedValue = null;
                    switch (type)
                    {
                        case "GUID":
                            // Для GUID формируем EntityReference
                            formatedValue = new EntityReference(entityName, Guid.Parse(value));
                            break;

                        case "URL":
                            // Для URL формируем EntityReference
                            formatedValue = new EntityReference(entityName, _helper.GetIdFromUrl(value));
                            break;

                        case "DATE":
                            // Для DATE преобразуем в DateTime
                            formatedValue = DateTime.Parse(value);
                            break;

                        case "BOOL":
                            // Для BOOL преобразуем в соответствующий логический тип
                            switch (value.ToLower())
                            {
                                case "да":
                                    formatedValue = true;
                                    break;
                                case "нет":
                                    formatedValue = false;
                                    break;
                                default:
                                    formatedValue = _helper.GetBooleanValueByLabel(service, entityName, name, value);
                                    break;
                            }
                            break;

                        case "LIST":
                            // Для LIST формируем OptionSetValue
                            formatedValue = new OptionSetValue(int.Parse(_helper.GetPicklistCode(service, entityName, name, value)));
                            break;

                        case "MONEY":
                            // Для MONEY получаем фактическое значение и преобразуем в decimal
                            Match matchMoney = Regex.Match(value, @"[\d\s,]+");
                            formatedValue = decimal.Parse(matchMoney.Value);
                            break;

                        default:
                            // В остальных случаев возвращаем значения в исходном виде
                            formatedValue = value;
                            break;
                    }

                    // Добавляем поле с корректным значеним в итоговый словарь
                    result.Add(name, formatedValue);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException($"Произошла ошибка при обработке условий из параметра 'Condition Attributes': {attributes}; ");
            }

            return result;
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
            inputArguments.AppendLine($"Values To Set: {ValuesToSet.Get(context.CodeActivityContext)}");
            inputArguments.AppendLine($"Amount Records To Process: {AmountRecordsToProcess.Get(context.CodeActivityContext)}");
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
            outputArguments.AppendLine($"Возникшие в процессе обновления записей ошибки:\n{_externalErrors}");
            _resultText.AppendLine(outputArguments.ToString());
        }
    }
}
