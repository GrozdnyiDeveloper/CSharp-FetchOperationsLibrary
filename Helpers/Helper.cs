using CRMPark.FetchOperations.Base;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CRMPark.FetchOperations.Helpers
{
    public class Helper
    {
        /// <summary>
        /// Метод получения GUID записи из URL
        /// </summary>
        /// <param name="recordURL">URL записи</param>
        public Guid GetIdFromUrl(string recordURL)
        {
            if (string.IsNullOrEmpty(recordURL))
            {
                return Guid.Empty;
            }

            Match match = Regex.Match(recordURL, "[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}", RegexOptions.IgnoreCase);
            return match.Success ? new Guid(match.Value) : Guid.Empty;
        }

        /// <summary>
        /// Метод получения записи Параметра по ключу
        /// </summary>
        /// <param name="service">Сервис к CRM</param>
        /// <param name="settingKey">Ключ Параметра</param>
        public Entity GetSettingByKey(IOrganizationService service, string settingKey)
        {
            try
            {
                // Выдаём ошибку при отсутствии ключа
                if (settingKey == null)
                {
                    throw new InvalidPluginExecutionException("Не передан обязательный параметр 'Setting Key'");
                }

                // Формируем запрос на получение записи Параметра по ключу
                QueryExpression query = new QueryExpression("crmpark_option");
                query.ColumnSet = new ColumnSet("crmpark_name", "crmpark_key", "crmpark_option_typecode", "crmpark_log_typecode", "crmpark_text_value", "crmpark_integer_value", "crmpark_logic_valuebit", "crmpark_query", "crmpark_layout");
                query.Criteria.AddCondition("crmpark_key", ConditionOperator.Equal, settingKey);
                var result = service.RetrieveMultiple(query);

                // Возвращаем ошибку или полученную запись в зависимости от результата
                if (result.Entities.Count > 0)
                {
                    return result.Entities[0];
                }
                else
                {
                    throw new InvalidPluginExecutionException("Не удалось получить запрос по переданному 'Setting Key'");
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        /// <summary>
        /// Метод получения фактического значения параметра из поля набора параметров по метке
        /// </summary>
        /// <param name="service">Сервис к CRM</param>
        /// <param name="entityLogicalName">Логическое имя сущности</param>
        /// <param name="picklistFieldName">Логическое имя поля</param>
        /// <param name="label">Метка</param>
        /// <returns></returns>
        public string GetPicklistCode(IOrganizationService service, string entityLogicalName, string picklistFieldName, string label)
        {
            var request = new RetrieveAttributeRequest
            {
                EntityLogicalName = entityLogicalName,
                LogicalName = picklistFieldName,
                RetrieveAsIfPublished = true
            };

            var response = (RetrieveAttributeResponse)service.Execute(request);
            var picklistMetadata = (PicklistAttributeMetadata)response.AttributeMetadata;

            foreach (OptionMetadata option in picklistMetadata.OptionSet.Options)
            {
                // Получаем локализованную метку
                string currentLabel = option.Label?.UserLocalizedLabel?.Label;
                if (currentLabel != null && currentLabel.Equals(label, StringComparison.InvariantCultureIgnoreCase))
                {
                    return option.Value.Value.ToString();
                }
            }

            return null;
        }

        /// <summary>
        /// Метод получения метке параметра из поля набора параметров по его фактическому значению
        /// </summary>
        /// <param name="service">Сервис к CRM</param>
        /// <param name="entityLogicalName">Логическое имя сущности</param>
        /// <param name="picklistFieldName">Логическое имя поля</param>
        /// <param name="value">Фактичекое значение</param>
        /// <returns></returns>
        public string GetPicklistLabel(IOrganizationService service, string entityLogicalName, string picklistFieldName, int value)
        {
            var attributeRequest = new RetrieveAttributeRequest
            {
                EntityLogicalName = entityLogicalName,
                LogicalName = picklistFieldName,
                RetrieveAsIfPublished = true
            };

            var attributeResponse = (RetrieveAttributeResponse)service.Execute(attributeRequest);
            var picklistMetadata = (PicklistAttributeMetadata)attributeResponse.AttributeMetadata;

            // Получаем метку
            foreach (OptionMetadata option in picklistMetadata.OptionSet.Options)
            {
                if (option.Value == value)
                {
                    return option.Label?.UserLocalizedLabel?.Label.ToString();
                }
            }

            return null;
        }

        /// <summary>
        /// Метод получения значения Boolean (Два параметра) по метке
        /// </summary>
        /// <param name="service">Сервис к CRM</param>
        /// <param name="entityLogicalName">Логическое имя сущности</param>
        /// <param name="booleanFieldName">Название поля</param>
        /// <param name="label">Метка: "Да"/"Нет" или "True"/"False"</param>
        /// <returns>true/false или null если не найдено</returns>
        public bool? GetBooleanValueByLabel(IOrganizationService service, string entityLogicalName, string booleanFieldName, string label)
        {
            var request = new RetrieveAttributeRequest
            {
                EntityLogicalName = entityLogicalName,
                LogicalName = booleanFieldName,
                RetrieveAsIfPublished = true
            };

            var response = (RetrieveAttributeResponse)service.Execute(request);
            var booleanMetadata = (BooleanAttributeMetadata)response.AttributeMetadata;

            // Получаем метки для True и False
            string trueLabel = booleanMetadata.OptionSet?.TrueOption?.Label?.UserLocalizedLabel?.Label;
            string falseLabel = booleanMetadata.OptionSet?.FalseOption?.Label?.UserLocalizedLabel?.Label;

            if (!string.IsNullOrEmpty(trueLabel) && trueLabel.Equals(label, StringComparison.InvariantCultureIgnoreCase))
                return true;

            if (!string.IsNullOrEmpty(falseLabel) && falseLabel.Equals(label, StringComparison.InvariantCultureIgnoreCase))
                return false;

            return null; // Не найдено
        }

        /// <summary>
        /// Метод проверки на то, является ли значение числовым
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool IsNumericType(Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Метод преобразования входной строки параметров действия в список параметров и их значений
        /// </summary>
        /// <param name="actionInputParameters">Входная строка параметров действия</param>
        /// <returns>Список параметров</returns>
        public Dictionary<string, string> SplitParameters(string actionInputParameters)
        {
            return actionInputParameters.Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries).Select(part => part.Split(new[] { '=' }, 2)).ToDictionary(split => split[0], split => split[1]);
        }

        /// <summary>
        /// Метод формирования запроса на запуск действия для указанной записи с передачей заданных параметров
        /// </summary>
        /// <param name="action">Название действия</param>
        /// <param name="entity">Запись по которой вызывается действие</param>
        /// <param name="parameters">Входные параметры действия</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public OrganizationRequest PrepareActionCallRequest(string action, EntityReference entity, Dictionary<string, string> parameters)
        {
            // Формируем запрос на запуск по переданному названию и указываем требуюмую запись 
            OrganizationRequest organizationRequest = new OrganizationRequest(action);
            organizationRequest["Target"] = entity;

            // Если были переданы входные параметры действия
            if (parameters != null && parameters.Count > 0)
            {
                // Для каждого параметра
                foreach (KeyValuePair<string, string> parameter in parameters)
                {
                    // Если для параметра указан тип данных
                    Match match = Regex.Match(parameter.Key, "(?<={).+(?=})");
                    if (match.Success)
                    {
                        // Получаем указанный в значении параметра тип данных
                        var type = match.Value.ToLower();
                        string parameterKey = Regex.Replace(parameter.Key, "{.+}", string.Empty);

                        // Если фактического значения для параметра не указано
                        if (string.IsNullOrEmpty(parameter.Value))
                        {
                            // Указываем пустое значение для параметра
                            organizationRequest[parameterKey] = null;
                        }
                        else
                        {
                            // Иначе получаем значение в исходном виде
                            string parameterValue = parameter.Value.Trim();

                            // В зависимости от указанного типа преобразуем в него исходное значение и добавляем в запрос
                            switch (type)
                            {
                                case "int":
                                    parameterValue = Regex.Match(Regex.Replace(parameterValue, @"\s+", ""), @"[\d.,]+").Value.Replace(',', '.');
                                    if (int.TryParse(parameterValue, NumberStyles.Any, CultureInfo.InvariantCulture, out int intResult))
                                    {
                                        organizationRequest[parameterKey] = intResult;
                                    }
                                    else
                                    {
                                        throw new InvalidPluginExecutionException($"Не удалось конвертировать указанное значение {parameterValue} в тип integer. ");
                                    }
                                    //organizationRequest[parameterKey] = int.Parse(parameterValue);
                                    break;
                                case "dec":
                                    parameterValue = Regex.Match(Regex.Replace(parameterValue, @"\s+", ""), @"[\d.,]+").Value.Replace(',', '.');
                                    if (decimal.TryParse(parameterValue, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal decimalResult))
                                    {
                                        organizationRequest[parameterKey] = decimalResult;
                                    }
                                    else
                                    {
                                        throw new InvalidPluginExecutionException($"Не удалось конвертировать указанное значение {parameterValue} в тип decimal. ");
                                    }
                                    //organizationRequest[parameterKey] = Decimal.Parse(parameterValue);
                                    break;
                                case "bool":
                                    if (bool.TryParse(parameterValue, out bool boolResult))
                                    {
                                        organizationRequest[parameterKey] = boolResult;
                                    }
                                    else
                                    {
                                        throw new InvalidPluginExecutionException($"Не удалось конвертировать указанное значение {parameterValue} в тип boolean. ");
                                    }
                                    //organizationRequest[parameterKey] = bool.Parse(parameterValue);
                                    break;
                                case "date":
                                    if (DateTime.TryParse(parameterValue, out DateTime dateResult))
                                    {
                                        organizationRequest[parameterKey] = dateResult;
                                    }
                                    else
                                    {
                                        throw new InvalidPluginExecutionException($"Не удалось конвертировать указанное значение {parameterValue} в тип date. ");
                                    }
                                    //organizationRequest[parameterKey] = DateTime.Parse(parameterValue);
                                    break;
                                default:
                                    throw new InvalidOperationException($"Указанный тип параметра {type} не поддерживается");
                            }
                        }
                    }
                    else
                    {
                        // Иначе добавляем параметр в запрос в исходном виде
                        organizationRequest[parameter.Key] = parameter.Value;
                    }

                }
            }

            // возвращаем сформированный запрос
            return organizationRequest;
        }

        /// <summary>
        /// Метод проверки существования указанной записи в CRM
        /// </summary>
        /// <param name="service">Сервис к CRM</param>
        /// <param name="entity">Запись в CRM</param>
        /// <exception cref="InvalidPluginExecutionException"></exception>
        public void CheckEntityExistance(IOrganizationService service, EntityReference entity)
        {
            try
            {
                var retrieveRequest = new RetrieveRequest()
                {
                    Target = entity,
                    ColumnSet = new ColumnSet(false)
                };
                service.Execute(retrieveRequest);
            }
            catch (Exception ex)
            {
                // Запись не найдена - выбрасываем исключение
                throw new InvalidPluginExecutionException($"Операция не может быть выполнена, так как запись {entity.LogicalName} с ID {entity.Id} не существует в системе. ", ex);
            }
        }

        public string GetPrimaryEntityForWorkflow(IOrganizationService service, EntityReference workflowReference)
        {
            var workflow = service.Retrieve(
                workflowReference.LogicalName,
                workflowReference.Id,
                new ColumnSet("primaryentity")
            );

            string primaryEntity = workflow.GetAttributeValue<string>("primaryentity");

            return primaryEntity;
        }
    }
}
