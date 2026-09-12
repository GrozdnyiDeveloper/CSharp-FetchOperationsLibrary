using CRMPark.FetchOperations.Base;
using CRMPark.FetchOperations.Helpers;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace CRMPark.FetchOperations.Search
{
    public class GetWorkflowDependencies : SearchStepBase
    {
        [RequiredArgument]
        [Input("Separator")]
        public InArgument<string> Separator { get; set; }

        [Output("Workflow Names")]
        public OutArgument<string> WorkflowNames { get; set; }

        public GetWorkflowDependencies()
        {
            _workflowName = "GetWorkflowDependencies";
        }

        protected override void ExecuteActivity(WorkflowActivityContext context, IOrganizationService service, string key)
        {
            // Получаем входные параметры шага
            var separator = Separator.Get(context.CodeActivityContext);

            // Производим попытку получения записи Параметра по указанному ключу
            var workflowsNames = GetDependenciesForWorkflow(service, key);

            // Формируем значения для выходных параметров (с учётом разделителя)
            WorkflowNames.Set(context.CodeActivityContext, workflowsNames.Count == 0 ? string.Empty : string.Join(separator, workflowsNames));
        }

        /// <summary>
        /// Метод получения с зависимостей Бизнес-шагов
        /// </summary>
        /// <param name="service">Сервис CRM</param>
        /// <param name="key">Название бизнес-шаг</param>
        public List<string> GetDependenciesForWorkflow(IOrganizationService service, string key)
        {
            // Преобразуем введённый ключ в корректный для поиска формат
            string wrappedKeyForSearch = ", \"" + key + "\", ";

            // Создаем и выполняем запрос для поиска бизнес-процессов/действий, использующих кастомный шаг по указанному ключу 
            // Используем Fetch-запрос, так как стандартный QueryExpression не поддерживает запросы к workflow
            string fetchXml = $@"
            <fetch>
                <entity name='workflow'>
                    <attribute name='name' />
                    <filter>
                        <condition attribute='type' operator='eq' value='1' />
                        <condition attribute='xaml' operator='like' value='%{wrappedKeyForSearch}%' />
                    </filter>
                </entity>
            </fetch>";

            var results = service.RetrieveMultiple(new FetchExpression(fetchXml));

            // Получаем результат и извлекаем из него названия всех найденных бизнес-процессов/действий
            return results.Entities
                .Select(entity => entity.GetAttributeValue<string>("name"))
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList();
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
            inputArguments.AppendLine($"Separator: {Separator.Get(context.CodeActivityContext)}");
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
            outputArguments.AppendLine($"Workflow Names: {WorkflowNames.Get(context.CodeActivityContext)}");
            _resultText.AppendLine(outputArguments.ToString());
        }
    }
}
