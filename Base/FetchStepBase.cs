using CRMPark.FetchOperations.Base;
using CRMPark.FetchOperations.Helpers;
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
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;

namespace CRMPark.FetchOperations.Base
{
    public abstract class FetchStepBase : MainStepBase
    {
        [RequiredArgument]
        [Input("Setting Key")]
        public InArgument<string> SettingKey { get; set; }

        [Input("Condition Attributes")]
        public InArgument<string> ConditionAttributes { get; set; }

        [Output("Count")]
        public OutArgument<int> Count { get; set; }
        public Entity _setting { get; set; }
        public string _fetchXml { get; set; }

        public FetchStepBase()
        {
            _setting = new Entity();
        }

        protected override void ExecuteActivity(WorkflowActivityContext context, IOrganizationService service)
        {
            var attributeNames = new List<string>();
            _receivedAmount = 0;
            _processedAmount = 0;

            // Получаем обработанный запрос и отправляем его в CRM
            (_fetchXml, attributeNames) = GetFormatedFetch(context, service);
            var entites = ProcessFetch(context, service);
            Count.Set(context.CodeActivityContext, entites.Count);
            _receivedAmount = entites.Count;

            ExecuteActivity(context, service, attributeNames, entites);
        }

        protected abstract void ExecuteActivity(
            WorkflowActivityContext context,
            IOrganizationService service,
            List<string> attributeNames,
            DataCollection<Entity> entites
        );

        /// <summary>
        /// Метод записи лога работы шага/параметра
        /// </summary>
        /// <param name="context">Контекст кастомного шага</param>
        /// <param name="service">Сервис к CRM</param>
        /// <param name="error">Возникшая ошибка</param>
        /// <param name="isSettingLog">Лог по параметру?</param>
        /// <param name="isEmptyLog">Лог по пустому результату?</param>
        /// <param name="previousEntity">Запись с заданными параметрами</param>
        public override void SaveLog(WorkflowActivityContext context, IOrganizationService service, Exception error = null, bool isSettingLog = false, bool isEmptyLog = false, Entity previousEntity = null)
        {
            // Задаём параметры для лога, связанные с параметром
            Entity log = new Entity("crmpark_operation");
            log["crmpark_name"] = isSettingLog ? "Option: " + _setting.GetAttributeValue<string>("crmpark_key") : "Workflow: " + _workflowName;
            log["crmpark_optionid"] = _setting != null ? new EntityReference(_setting.LogicalName, _setting.Id) : null;
            log["crmpark_query"] = _fetchXml ?? _setting?.GetAttributeValue<string>("crmpark_query");
            log["crmpark_layout"] = _setting?.GetAttributeValue<string>("crmpark_layout");
            base.SaveLog(context, service, error, isSettingLog, isEmptyLog, log);
        }

        /// <summary>
        /// Метод отправки Fetch-запроса и получения результата
        /// </summary>
        /// <param name="context">Контекст кастомного шага</param>
        /// <param name="service">Сервис к CRM</param>
        /// <param name="fetchXml">Fetch-запрос</param>
        /// <returns>Список полученных записей</returns>
        public DataCollection<Entity> ProcessFetch(WorkflowActivityContext context, IOrganizationService service)
        {
            var logType = _setting.GetAttributeValue<OptionSetValue>("crmpark_log_typecode");

            try
            {
                if (string.IsNullOrWhiteSpace(_fetchXml))
                {
                    throw new InvalidPluginExecutionException("Был получен пустой Fetch запрос");
                }

                // Отправляем fetch-запрос в CRM и фиксируем полученное кол-во записей
                var result = service.RetrieveMultiple(new FetchExpression(_fetchXml));

                var resultText = $"Полученные по параметру записи:\n" +
                    $"{string.Join(";\n", result.Entities.Select(e => e.Id.ToString()))}";

                // Отправляем лог работы по параметру (если логирование включено)
                SaveLog(
                    context: context,
                    service: service,
                    isEmptyLog: result.Entities.Count == 0,
                    isSettingLog: true
                );
                return result.Entities;
            }
            catch (Exception ex)
            {
                // Создание лога в случаи ошибки
                _innerErrors.AppendLine($"Произошла ошибка при получении записей из CRM согласно Fetch-запросу; ");
                SaveLog(
                    context: context,
                    service: service,
                    error: ex,
                    isEmptyLog: true,
                    isSettingLog: true
                );
                throw;
            }
        }

        /// <summary>
        /// Метод получения обработанного Fetch-запроса
        /// </summary>
        /// <param name="context">Контекст кастомного шага</param>
        /// <param name="service">Сервис CRM</param>
        /// <returns>Обработанный Fetch запрос</returns>
        public (string, List<string>) GetFormatedFetch(WorkflowActivityContext context, IOrganizationService service)
        {
            var fetchXml = string.Empty;
            var layoutXml = string.Empty;
            List<string> attributeNames = new List<string>();

            // Получаем ключ записи Параметра из входного параметра
            var settingName = SettingKey.Get(context.CodeActivityContext);
            if (string.IsNullOrWhiteSpace(settingName))
            {
                throw new InvalidPluginExecutionException("Не был заполнен входной параметр 'Setting Key'");
            }

            // Получаем запись Параметра по ключу
            _setting = _helper.GetSettingByKey(service, settingName);
            if (_setting == null)
            {
                throw new InvalidPluginExecutionException("Не удалось получить запись Параметра по переданному ключу");
            }

            try
            {
                // Получаем из записи Параметра значения Fetch запроса и Layout макета
                fetchXml = _setting.GetAttributeValue<string>("crmpark_query");
                layoutXml = _setting.GetAttributeValue<string>("crmpark_layout");

                // Заменяем в Fetch запросе получаемые атрибуты на указанные в Layout макете 
                attributeNames = GetAttributeNamesFromLayout(layoutXml);
                fetchXml = ReplaceAttributesInFetch(fetchXml, attributeNames);

                // Добавляем в запрос переданыне в шаг значения условий
                var conditons = ConditionAttributes.Get(context.CodeActivityContext);
                fetchXml = PasteConditionsInFetch(service, fetchXml, conditons);
            }
            catch (Exception ex)
            {
                _innerErrors.AppendLine($"Произошла ошибка при обработке fetch-запроса; ");
                throw;
            }

            return (fetchXml, attributeNames);
        }

        /// <summary>
        /// Извлекает названия атрибутов из Layout
        /// </summary>
        /// <param name="layoutXml">Текст Layout XML</param>
        /// <returns>Список названий атрибутов</returns>
        public List<string> GetAttributeNamesFromLayout(string layoutXml)
        {
            // Если не передан макет, то возвращаем пустой список
            var attributeNames = new List<string>();
            if (string.IsNullOrWhiteSpace(layoutXml))
            {
                return attributeNames;
            } 

            try
            {
                // Через XDocument получаем значения ячеек (атрибутов) из макета
                var doc = XDocument.Parse(layoutXml);
                var cellElements = doc.Descendants("cell")?.Where(cell => cell.Attribute("name") != null);
                foreach (var cell in cellElements)
                {
                    // Из кахдой ячейки получаем и сохраняем имя
                    string attributeName = cell.Attribute("name")?.Value;
                    if (!string.IsNullOrEmpty(attributeName))
                    {
                        attributeNames.Add(attributeName);
                    }
                }
            }
            catch (Exception ex)
            {
                _innerErrors.AppendLine($"Ошибка при парсинге LayoutXML; ");
                throw;
            }

            return attributeNames;
        }

        /// <summary>
        /// Заменяет все атрибуты из Fetch на атрибуты из Layout
        /// </summary>
        /// <param name="fetchXml">Исходный fetch-запрос в виде строки</param>
        /// <param name="attributeNames">Список новых атрибутов</param>
        /// <returns>Модифицированный fetch-запрос</returns>
        public string ReplaceAttributesInFetch(string fetchXml, List<string> attributeNames)
        {
            if (string.IsNullOrWhiteSpace(fetchXml))
            {
                throw new InvalidPluginExecutionException("Был передан пустой Fetch запрос");
            }

            // Если не были получены атрибуты из Layout, то не заменяем их и возвращаем исходный fetch
            if (attributeNames == null || attributeNames.Count == 0)
            {
                return fetchXml;
            }

            // Находим элемент fetch
            XDocument doc = XDocument.Parse(fetchXml);
            XElement fetchElement = doc.Root;
            if (fetchElement == null || fetchElement.Name != "fetch")
            {
                throw new InvalidPluginExecutionException("Fetch запрос имеет некорректный формат: Корневой элемент должен быть <fetch>");
            }

            // Находим элемент entity
            XElement entityElement = fetchElement.Element("entity");
            if (entityElement == null)
            {
                throw new InvalidPluginExecutionException("Fetch запрос имеет некорректный формат: Элемент <entity> не найден");
            }

            // Удаляем все существующие элементы <attribute>
            entityElement.Elements("attribute").Remove();

            // Добавляем новые атрибуты
            foreach (string attributeName in attributeNames)
            {
                if (!string.IsNullOrWhiteSpace(attributeName))
                {
                    entityElement.Add(new XElement("attribute", new XAttribute("name", attributeName)));
                }
            }

            return doc.ToString(SaveOptions.DisableFormatting);
        }

        /// <summary>
        /// Метод подстановки значений условий переданных в шаг
        /// </summary>
        /// <param name="service">Сервис CRM</param>
        /// <param name="fetchXml">Исходный fetch-запрос</param>
        /// <param name="conditions">Полученные шагом строка значений условий</param>
        /// <returns>Модифицированный fetch-запрос</returns>
        public string PasteConditionsInFetch(IOrganizationService service, string fetchXml, string conditions)
        {
            if (string.IsNullOrWhiteSpace(fetchXml))
            {
                throw new InvalidPluginExecutionException("Был передан пустой Fetch запрос");
            }

            // Если не были получены значения для условий, то просто не заменяем их и возвращаем исходный fetch
            if (string.IsNullOrEmpty(conditions))
            {
                return fetchXml;
            }

            // Находим элемент fetch
            XDocument doc = XDocument.Parse(fetchXml);
            XElement fetchElement = doc.Root;
            if (fetchElement == null || fetchElement.Name != "fetch")
            {
                throw new InvalidPluginExecutionException("Fetch запрос имеет некорректный формат: Корневой элемент должен быть <fetch>");
            }

            // Находим элемент entity
            XElement entityElement = fetchElement.Element("entity");
            if (entityElement == null)
            {
                throw new InvalidPluginExecutionException("Fetch запрос имеет некорректный формат: Элемент <entity> не найден");
            }

            // Получаем логическое имя сущности
            var entityName = entityElement.Attribute("name").Value;

            // Получаем список условий со значениями в корректном формате. Если значений не было передано, то возвращаем исходный fetch
            var conditionsValues = FormateConditionValues(service, conditions, entityName);
            if (conditionsValues == null || conditionsValues.Count == 0)
            {
                throw new InvalidPluginExecutionException ($"Не удалось обработать условия из параметра 'Condition Attributes': {conditions}; ");
            }

            // Находим элемент filter. Если не нашли, то возвращаем исходный fetch
            XElement filterElement = entityElement.Element("filter");
            if (filterElement == null)
            {
                return fetchXml;
            }

            // Добавляем новые атрибуты в Fetch на основе полученного списка условий
            foreach (XElement condition in filterElement.Elements("condition"))
            {
                if (conditionsValues.TryGetValue(condition.Attribute("attribute").Value, out var value))
                {
                    // Заменяем значенеи и дополняем его на случай, если используется оператор like
                    var operation = condition.Attribute("operator").Value;
                    var oldValue = condition.Attribute("value").Value;
                    if (operation == "like" || operation == "not-like")
                    {
                        if (oldValue.StartsWith("%"))
                            value = value.Insert(0, "%");
                        if (oldValue.EndsWith("%"))
                            value = value.Insert(value.Length, "%");
                    }
                    condition.Attribute("value").SetValue(value);
                }
            }

            // Возвращаем обновленный Fetch
            return doc.ToString(SaveOptions.DisableFormatting);
        }

        /// <summary>
        /// Метод получения из переданной строки списка условий с корректными форматами значений
        /// </summary>
        /// <param name="service">Сервис CRM</param>
        /// <param name="conditions">Полученные шагом строка значений условий</param>
        /// <param name="entityName">Логическое имя сущности</param>
        /// <returns>Лист условий с корректными форматами значений</returns>
        public Dictionary<string, dynamic> FormateConditionValues(IOrganizationService service, string conditions, string entityName)
        {
            Dictionary<string, dynamic> result = new Dictionary<string, dynamic>();

            try
            {
                // Получаем все значения условий из текста
                List<string> conditionsValues = conditions.Split(new[] { "||" }, StringSplitOptions.None).ToList();
                foreach (string condition in conditionsValues)
                {
                    var parts = condition.Split(new[] { '=' }, 2);
                    if (parts == null || parts.Count() != 2)
                    {
                        throw new InvalidPluginExecutionException($"Условие {condition} указано в некорректном формате");
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

                    // В зависимости от указанного типа получаем значение в корректном формате
                    dynamic formatedValue = null;
                    switch (type)
                    {
                        case "GUID":
                            formatedValue = Guid.Parse(value);
                            break;

                        case "URL":
                            formatedValue = _helper.GetIdFromUrl(value);
                            break;

                        case "DATE":
                            formatedValue = DateTime.Parse(value).ToString("yyyy-MM-dd");
                            break;

                        case "BOOL":
                            switch (value.ToLower())
                            {
                                case "да":
                                    formatedValue = "1";
                                    break;
                                case "нет":
                                    formatedValue = "0";
                                    break;
                                default:
                                    formatedValue = _helper.GetBooleanValueByLabel(service, entityName, name, value);
                                    break;
                            }
                            break;

                        case "LIST":
                            formatedValue = _helper.GetPicklistCode(service, entityName, name, value);
                            break;

                        case "MONEY":
                            Match matchMoney = Regex.Match(value, @"[\d\s,]+");
                            formatedValue = decimal.Parse(matchMoney.Value);
                            break;

                        default:
                            formatedValue = value;
                            break;
                    }

                    result.Add(name, formatedValue);
                }
            }
            catch (Exception ex)
            {
                _innerErrors.AppendLine($"Произошла ошибка при обработке условий из параметра 'Condition Attributes': {conditions}; ");
                throw;
            }

            return result;
        }

        /// <summary>
        /// Метод указания входных параметров шага, наследуемый из Main
        /// </summary>
        /// <param name="context">Контекст CRM</param>
        protected override void GetInputArguments(WorkflowActivityContext context)
        {
            // Получаем строку от Main
            base.GetInputArguments(context);
            StringBuilder inputArguments = new StringBuilder();

            // Добавляем параметры шагов группы Fetch
            inputArguments.AppendLine($"Setting Key: {SettingKey.Get(context.CodeActivityContext)}");
            inputArguments.AppendLine($"Condition Attributes: {ConditionAttributes.Get(context.CodeActivityContext)}");
            _resultText.Append(inputArguments.ToString());
        }
    }
}
