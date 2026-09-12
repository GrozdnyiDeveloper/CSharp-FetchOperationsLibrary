using CRMPark.FetchOperations.Helpers;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Description;
using System.Text;
using System.Threading.Tasks;

namespace CRMPark.FetchOperations.Base
{
    public abstract class MainStepBase : CodeActivity
    {
        [Input("Log Key")]
        public InArgument<string> LogKey { get; set; }

        [RequiredArgument]
        [Input("Log Type")]
        [AttributeTarget("crmpark_option", "crmpark_log_typecode")]
        public InArgument<OptionSetValue> LogType { get; set; }

        [Input("Log Message")]
        public InArgument<string> LogMessage { get; set; }

        [Input("Responsible User")]
        [ReferenceTarget("systemuser")]
        public InArgument<EntityReference> ResponsibleUser { get; set; }

        [Output("Result Code")]
        public OutArgument<bool> ResultCode { get; set; }

        [Output("Error Text")]
        public OutArgument<string> ErrorText { get; set; }

        public enum LogTypeList
        {
            All = 557180000,
            ErrorsOnly = 557180001,
            NonEmpty = 557180002,
            None = 557180003
        }

        public enum OptionType
        {
            Fetch = 557180000,
            Math = 557180001,
            Business = 557180002
        }

        public DateTime _startTime { get; set; } // Время начала работы шага
        public Guid _responsibleUserId { get; set; } // Ответственный за выполнение шага
        public int _workflowLogTypeCode { get; set; } // Тип логирования шага
        public string _workflowName { get; set; } // Название шага
        public Helper _helper { get; set; } // Хелпер с общими вспомогательными функциями
        public StringBuilder _innerErrors { get; set; } // Стэк внутрениих ошибок (методы Fetch)
        public StringBuilder _externalErrors { get; set; } // Стэк внешних ошибок (ошибки в вызываемых действиях)
        public StringBuilder _resultText { get; set; } // Текст результата работы шага
        public int? _receivedAmount { get; set; } // Количество полученных записей 
        public int? _processedAmount { get; set; } // Количество успешно обработанных записей 

        public MainStepBase()
        {
            _helper = new Helper();
            _workflowName = string.Empty;
            _innerErrors = new StringBuilder();
            _externalErrors = new StringBuilder();
            _resultText = new StringBuilder();
        }

        protected override void Execute(CodeActivityContext context)
        {
            try
            {
                // Создание контекста шага и задание глобальных параметров "Время запуска", "Инициализатор шага" и "Тип логирования шага"
                var workflowContext = new WorkflowActivityContext(context);
                _startTime = DateTime.UtcNow;
                _responsibleUserId = ResponsibleUser.Get(context)?.Id ?? workflowContext.InitiatingUserId;
                _workflowLogTypeCode = LogType.Get(context).Value;
                var service = workflowContext.GetOrganizationService(_responsibleUserId);
                try
                {
                    // Действия перед началом работы метода
                    BeforeExecuteActivity(workflowContext, service);

                    // Запись входных параметров
                    GetInputArguments(workflowContext);

                    // Запуск основной логики
                    ExecuteActivity(workflowContext, service);

                    // Запись выходных параметров
                    GetOutputArguments(workflowContext);

                    // Создаём запись лога по результатам работы
                    SaveLog(
                        context: workflowContext,
                        service: service,
                        isEmptyLog: _processedAmount != null ? _processedAmount == 0 : false
                    );
                }
                catch (Exception ex)
                {
                    // Создание лога в случаи ошибки
                    SaveLog(
                        context: workflowContext,
                        service: service,
                        error: ex,
                        isEmptyLog: true
                    );
                    throw;
                }
                ResultCode.Set(context, true);
            }
            catch (Exception ex)
            {
                ResultCode.Set(context, false);
                ErrorText.Set(context, ex.ToString());
            }
        }

        protected abstract void ExecuteActivity(WorkflowActivityContext context, IOrganizationService service);

        protected virtual void BeforeExecuteActivity(WorkflowActivityContext context, IOrganizationService service) 
        {
            // Очистка информации шага на случай кеширования со стороны Dynamics
            _resultText.Clear();
            _innerErrors.Clear();
            _externalErrors.Clear();
            _processedAmount = null;
            _receivedAmount = null;
        }

        /// <summary>
        /// Метод записи лога работы шага/параметра
        /// </summary>
        /// <param name="context">Контекст кастомного шага</param>
        /// <param name="service">Сервис к CRM</param>
        /// <param name="error">Возникшая ошибка</param>
        /// <param name="isSettingLog">Лог по параметру?</param>
        /// <param name="isEmptyLog">Лог по пустому результату?</param>
        /// <param name="previousEntity">Запись с заданными параметрами</param>
        public virtual void SaveLog(WorkflowActivityContext context, IOrganizationService service, Exception error = null, bool isSettingLog = false, bool isEmptyLog = false, Entity previousEntity = null)
        {
            // Определяем результат работы по наличию ошибки
            var resultBit = error == null;

            // Отменяем запись лога если верно одно из условий:
            // 1) Тип логирования = "Не логировать"
            // 2) Тип логирования = "Непустые операции" И Результат работы удачный И Пустой лог
            // 3) Тип логирования = "Только ошибочные операции" И Результат работы удачный
            if (_workflowLogTypeCode == ((int)LogTypeList.None) || resultBit && ((_workflowLogTypeCode == ((int)LogTypeList.NonEmpty) && isEmptyLog) || _workflowLogTypeCode == ((int)LogTypeList.ErrorsOnly)))
            {
                return;
            }

            // Получаем ключ и сообщение для лога
            var key = LogKey.Get(context.CodeActivityContext);
            var message = LogMessage.Get(context.CodeActivityContext);
            var name = previousEntity == null ? "Workflow: " + _workflowName : previousEntity.GetAttributeValue<string>("crmpark_name");

            // Формируем сообщение для лога и создаём запись "Сессия операции"
            Entity log = previousEntity ?? new Entity("crmpark_operation");
            log["crmpark_key"] = key;
            log["crmpark_name"] = name;
            log["crmpark_message"] = message;
            log["ownerid"] = new EntityReference("systemuser", _responsibleUserId);
            log["crmpark_processedon"] = _startTime;
            log["crmpark_resultbit"] = resultBit;
            log["crmpark_result_text"] = error == null ? (_resultText?.ToString() ?? string.Empty) : string.Empty;
            log["crmpark_result_error"] = error != null ? (_resultText?.ToString() ?? string.Empty) + FormateError(error) : string.Empty;

            // Добавляем в лог кол-во обработанных записей если они имеются
            if (_receivedAmount.HasValue)
            {
                log["crmpark_received_records_quantity"] = _receivedAmount;
            }
            if (_processedAmount.HasValue)
            {
                log["crmpark_processed_records_quantity"] = _processedAmount;
            }

            service.Create(log);
        }

        /// <summary>
        /// Метод формирования сообщения об ошибке
        /// </summary>
        /// <param name="ex">Полученная ошибка</param>
        /// <returns>Сформриованное сообщение об ошибке</returns>
        public string FormateError(Exception ex)
        {
            // Выходим из метода если передали пустое значеие
            if (ex == null)
            {
                return null;
            }
            
            var errorMessage = new StringBuilder();

            // Добавляем в формируемое сообщение сам текст сообщения из ошибки и StackTrace
            errorMessage.AppendLine($"Произошла ошибка при работе кастомного шага: ");
            errorMessage.AppendLine($"Сообщение: {ex.Message}");
            errorMessage.AppendLine($"StackTrace: {ex.StackTrace}");

            // При наличии перехваченных ранее ошибок добавляем их в сообщение
            if (_innerErrors.Length > 0)
            {
                errorMessage.AppendLine($"Перехваченные ранее ошибки: {_innerErrors}");
            }

            // При наличии внутренних ошибок добавляем их в сообщение
            if (ex.InnerException != null)
            {
                errorMessage.AppendLine($"Inner Exception: {ex.InnerException.Message}");
                errorMessage.AppendLine($"Inner StackTrace: {ex.InnerException.StackTrace}");
            }

            return errorMessage.ToString();
        }

        /// <summary>
        /// Виртуальный метод указания входных параметров шага
        /// </summary>
        /// <param name="context">Контекст CRM</param>
        protected virtual void GetInputArguments(WorkflowActivityContext context)
        {
            // Базовый уровень - только заголовок
            StringBuilder inputArguments = new StringBuilder();
            inputArguments.AppendLine($"Входные аргументы шага:");
            _resultText.Append(inputArguments.ToString());
        }

        /// <summary>
        /// Виртуальный метод указания выходных значений параметров шага
        /// </summary>
        /// <param name="context">Контекст CRM</param>
        protected virtual void GetOutputArguments(WorkflowActivityContext context)
        {
            // Базовый уровень - только заголовок
            StringBuilder outputArguments = new StringBuilder();
            outputArguments.AppendLine($"Результат работы шага:");
            _resultText.Append(outputArguments.ToString());
        }
    }
}
