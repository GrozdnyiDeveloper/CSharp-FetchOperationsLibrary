using CRMPark.FetchOperations.Base;
using CRMPark.FetchOperations.Fetch;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.SqlServer.Server;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Workflow;
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
    public class RunActionFetchXMLRecords : FetchStepBase
    {
        [RequiredArgument]
        [Input("Action")]
        public InArgument<string> Action { get; set; }

        [Input("Action Input Parameters")]
        public InArgument<string> ActionInputParameters { get; set; }

        [Input("Amount Records To Process")]
        public InArgument<int> AmountRecordsToProcess { get; set; }

        [Output("Processed Records Quantity")]
        public OutArgument<int> ProcessedRecordsQuantity { get; set; }

        public RunActionFetchXMLRecords()
        {
            // Задаём в глобальную переменную название шага
            _workflowName = "RunActionFetchXMLRecords";
        }

        protected override void ExecuteActivity(WorkflowActivityContext context, IOrganizationService service, List<string> attributeNames, DataCollection<Entity> entites)
        {
            // Получаем входные параметры 
            var action = Action.Get(context.CodeActivityContext);
            var actionInputParameters = ActionInputParameters.Get(context.CodeActivityContext);
            var amountRecordsToProcess = AmountRecordsToProcess.Get(context.CodeActivityContext);

            // Если в результате fetch-запроса получили записи
            if (entites.Count != 0)
            {
                // Получаем сходные параметры для дейсвтий в удобном формате
                var parameters = _helper.SplitParameters(actionInputParameters);

                // Для каждой записи
                for (int i = 0; i < entites.Count && (amountRecordsToProcess == 0 || i < amountRecordsToProcess); i++)
                {
                    Entity entity = entites[i];
                    try
                    {
                        // Формируем запрос на вызов действия с передачей параметров
                        OrganizationRequest organizationRequest = _helper.PrepareActionCallRequest(action, entity.ToEntityReference(), parameters);
                        service.Execute(organizationRequest);
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
        /// Метод указания входных значений в текст результата
        /// </summary>
        /// <param name="context">Контекст CRM</param>
        protected override void GetInputArguments(WorkflowActivityContext context)
        {
            // Получаем наследуемую строку 
            base.GetInputArguments(context);
            StringBuilder inputArguments = new StringBuilder();
            inputArguments.AppendLine($"Action: {Action.Get(context.CodeActivityContext)}");
            inputArguments.AppendLine($"Action Input Parameters: {ActionInputParameters.Get(context.CodeActivityContext)}");
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
            outputArguments.AppendLine($"Возникшие в процессе работы действий ошибки:\n{_externalErrors}");
            _resultText.AppendLine(outputArguments.ToString());
        }
    }
}
