using CRMPark.FetchOperations.Base;
using CRMPark.FetchOperations.Fetch;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Workflow;
using Microsoft.Xrm.Sdk.Workflow.Activities;
using System;
using System.Activities;
using System.Activities.Statements;
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
    public class AssignFetchXMLRecords : FetchStepBase
    {
        [Input("Assign To User")]
        [ReferenceTarget("systemuser")]
        public InArgument<EntityReference> AssignToUser { get; set; }

        [Input("Assign To Team")]
        [ReferenceTarget("team")]
        public InArgument<EntityReference> AssignToTeam { get; set; }

        [Input("Amount Records To Process")]
        public InArgument<int> AmountRecordsToProcess { get; set; }

        [Output("Processed Records Quantity")]
        public OutArgument<int> ProcessedRecordsQuantity { get; set; }

        public AssignFetchXMLRecords()
        {
            // Задаём в глобальную переменную название шага
            _workflowName = "AssignFetchXMLRecords";
        }

        protected override void ExecuteActivity(WorkflowActivityContext context, IOrganizationService service, List<string> attributeNames, DataCollection<Entity> entites)
        {
            var amountRecordsToProcess = AmountRecordsToProcess.Get(context.CodeActivityContext);

            // Получаем нового ответственного для назначения из входных параметров (пользователя или рабочую группу)
            var assign = new EntityReference();
            assign = AssignToUser.Get(context.CodeActivityContext);
            if (assign == null)
            {
                assign = AssignToTeam.Get(context.CodeActivityContext);
                if (assign == null)
                {
                    // Выдаём ошибку если ответственный не был передан
                    throw new InvalidPluginExecutionException($"Не указана запись для назначения");
                }
            }

            // Если в результате fetch-запроса получили записи
            if (entites.Count != 0)
            {
                // Для каждой записи
                for (int i = 0; i < entites.Count && (amountRecordsToProcess == 0 || i < amountRecordsToProcess); i++)
                {
                    Entity entity = entites[i];
                    try
                    {
                        // Формируем запрос на назначение записи указанному ответственному
                        AssignRequest assignRequest = new AssignRequest();
                        assignRequest.Assignee = assign;
                        assignRequest.Target = entity.ToEntityReference();
                        service.Execute(assignRequest);
                        _processedAmount++;
                    }
                    catch (Exception ex)
                    {
                        // В случаи ошибки сохраняем её в список
                        _externalErrors.AppendLine(string.Format("{0} - {1}", entity.Id.ToString(), ex.Message));
                    }
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
            inputArguments.AppendLine($"Assign To User: {AssignToUser.Get(context.CodeActivityContext)?.Id}"); 
            inputArguments.AppendLine($"Assign To Team: {AssignToTeam.Get(context.CodeActivityContext)?.Id}");
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
