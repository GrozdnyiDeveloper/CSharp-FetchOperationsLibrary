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
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using static CRMPark.FetchOperations.Fetch.BindUnbindFetchXMLRecords;

namespace CRMPark.FetchOperations.Common
{
    public class RunWorkflowByRecordId : MainStepBase
    {
        [RequiredArgument]
        [Input("Workflow")]
        [ReferenceTarget("workflow")]
        public InArgument<EntityReference> Workflow { get; set; }

        [RequiredArgument]
        [Input("Record Ids")]
        public InArgument<string> RecordIds { get; set; }

        [Output("Processed Records Quantity")]
        public OutArgument<int> ProcessedRecordsQuantity { get; set; }

        public RunWorkflowByRecordId() 
        {
            _workflowName = "RunWorkflowByRecordId";
        }

        protected override void ExecuteActivity(WorkflowActivityContext context, IOrganizationService service)
        {
            _receivedAmount = 0;
            _processedAmount = 0;

            // Получаем входные параметры 
            var workflow = Workflow.Get(context.CodeActivityContext);
            var recordIds = RecordIds.Get(context.CodeActivityContext);

            // Проверяем заполнение обязательных параметров 
            if (workflow == null)
            {
                throw new InvalidPluginExecutionException($"Не был передан вызываемый бизнес-процесс. ");
            }
            else if (string.IsNullOrEmpty(recordIds))
            {
                throw new InvalidPluginExecutionException($"Не было указано ID записей для вызова действия. ");
            }

            // Получаем основную сущность для бизнес-процесса
            var primaryEntity = _helper.GetPrimaryEntityForWorkflow(service, workflow);

            // Получаем все указанные ID записей в параметре Record Id
            string[] recordIdsList = recordIds.Split(";".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            _receivedAmount = recordIdsList.Length;

            // Для каждого указанного в параметре Id записи
            foreach (var recordId in recordIdsList)
            {
                try
                {
                    if (!Guid.TryParse(recordId, out Guid recordGuid))
                    {
                        throw new InvalidPluginExecutionException($"Был указан некорректный Id записи {recordId}. ");
                    }

                    // Проверяем существование записи в CRM и её соответствие основной сущности бизнес-процесса
                    try
                    {
                        _helper.CheckEntityExistance(service, new EntityReference(primaryEntity, recordGuid));
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidPluginExecutionException($"Запись под указанным Id не существует или она не соответствует основной сущности {primaryEntity} бизнес-процесса. ");
                    }

                    // Формируем запрос на вызов бизнес-процесса
                    ExecuteWorkflowRequest executeWorkflowRequest = new ExecuteWorkflowRequest()
                    {
                        WorkflowId = workflow.Id,
                        EntityId = recordGuid
                    };
                    service.Execute(executeWorkflowRequest);
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
            inputArguments.AppendLine($"Workflow: {Workflow.Get(context.CodeActivityContext)?.Name}");
            inputArguments.AppendLine($"Record Ids: {RecordIds.Get(context.CodeActivityContext)}");
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
            outputArguments.AppendLine($"Возникшие в процессе работы бизнес-процессов ошибки:\n{_externalErrors}");
            _resultText.AppendLine(outputArguments.ToString());
        }
    }
}
