using CRMPark.FetchOperations.Base;
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
    public class RunActionByRecordId : MainStepBase
    {
        [RequiredArgument]
        [Input("Action")]
        public InArgument<string> Action { get; set; }

        [RequiredArgument]
        [Input("Record Ids")]
        public InArgument<string> RecordIds { get; set; }

        [RequiredArgument]
        [Input("Entity Name")]
        public InArgument<string> EntityName { get; set; }

        [Input("Action Input Parameters")]
        public InArgument<string> ActionInputParameters { get; set; }

        [Output("Processed Records Quantity")]
        public OutArgument<int> ProcessedRecordsQuantity { get; set; }

        public RunActionByRecordId() 
        {
            _workflowName = "RunActionByRecordId";
        }

        protected override void ExecuteActivity(WorkflowActivityContext context, IOrganizationService service)
        {
            _receivedAmount = 0;
            _processedAmount = 0;

            // Получаем входные параметры  
            var action = Action.Get(context.CodeActivityContext);
            var recordIds = RecordIds.Get(context.CodeActivityContext);
            var entityName = EntityName.Get(context.CodeActivityContext);
            var actionInputParameters = ActionInputParameters.Get(context.CodeActivityContext);

            // Проверяем заполнение обязательных параметров 
            if (string.IsNullOrEmpty(action))
            {
                throw new InvalidPluginExecutionException($"Не было указано название вызываемого действия Action. ");
            }
            else if (string.IsNullOrEmpty(recordIds) || string.IsNullOrEmpty(entityName))
            {
                throw new InvalidPluginExecutionException($"Не было указано ID записей для вызова действия и/или его логического имени. ");
            }

            // Получаем все указанные ID записей в параметре Record Id
            string[] recordIdsList = recordIds.Split(";".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            _receivedAmount = recordIdsList.Length;

            // Получаем сходные параметры для дейсвтий в удобном формате
            var parameters = _helper.SplitParameters(actionInputParameters);

            // Для каждого указанного в параметре Id записи
            foreach (var recordId in recordIdsList)
            {
                try
                {
                    if (!Guid.TryParse(recordId, out Guid recordGuid))
                    {
                        throw new InvalidPluginExecutionException($"Был указан некорректный Id записи {recordId}. ");
                    }

                    // Формируем запрос на вызов действия с передачей параметров
                    OrganizationRequest organizationRequest = _helper.PrepareActionCallRequest(action, new EntityReference(entityName, recordGuid), parameters);
                    service.Execute(organizationRequest);
                    _processedAmount++;
                }
                catch (Exception ex)
                {
                    // Добавление возникших ошибок по каждой записи из запроса 
                    _externalErrors.AppendLine(string.Format("{0} - {1}", recordId, ex.Message));
                }
            }

            // Фиксируем кол-во успешно обработанных записей
            ProcessedRecordsQuantity.Set(context.CodeActivityContext, _processedAmount);
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
            inputArguments.AppendLine($"Action: {Action.Get(context.CodeActivityContext)}");
            inputArguments.AppendLine($"Record Ids: {RecordIds.Get(context.CodeActivityContext)}");
            inputArguments.AppendLine($"Entity Name: {EntityName.Get(context.CodeActivityContext)}");
            inputArguments.AppendLine($"Action Input Parameters: {ActionInputParameters.Get(context.CodeActivityContext)}");
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
            outputArguments.AppendLine($"Возникшие в процессе работы действий ошибки:\n{_externalErrors}");
            _resultText.AppendLine(outputArguments.ToString());
        }
    }
}
