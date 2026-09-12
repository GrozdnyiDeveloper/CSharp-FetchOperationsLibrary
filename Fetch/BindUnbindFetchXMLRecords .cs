using CRMPark.FetchOperations.Base;
using CRMPark.FetchOperations.Fetch;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Workflow;
using Microsoft.Xrm.Sdk.Workflow.Activities;
using System;
using System.Activities;
using System.Activities.Expressions;
using System.Activities.Statements;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Xml.Linq;

namespace CRMPark.FetchOperations.Fetch
{
    public class BindUnbindFetchXMLRecords : FetchStepBase
    {
        [RequiredArgument]
        [Input("Record URL/GUID")]
        public InArgument<string> RecordId { get; set; }

        [RequiredArgument]
        [Input("Entity Name")]
        public InArgument<string> EntityName { get; set; }

        [RequiredArgument]
        [Input("Relation Name")]
        public InArgument<string> RelationName { get; set; }

        [RequiredArgument]
        [Input("Operation")]
        [AttributeTarget("crmpark_option", "crmpark_bind_operation_typecode")]
        public InArgument<OptionSetValue> Operation { get; set; }

        [Input("Amount Records To Process")]
        public InArgument<int> AmountRecordsToProcess { get; set; }

        [Output("Processed Records Quantity")]
        public OutArgument<int> ProcessedRecordsQuantity { get; set; }

        public enum BindType
        {
            Bind = 557180000,
            Unbind = 557180001,
            AddToMarketingList = 557180002,
            RemoveFromMarketingList = 557180003
        }

        public BindUnbindFetchXMLRecords()
        {
            // Задаём в глобальную переменную название шага
            _workflowName = "BindUnbindFetchXMLRecords";
        }

        public string _operationLabel { get; set; }

        protected override void BeforeExecuteActivity(WorkflowActivityContext context, IOrganizationService service)
        {
            // Получаем значение метки параметра из входного набора параметров
            var operation = Operation.Get(context.CodeActivityContext);
            if (operation != null)
            {
                _operationLabel = _helper.GetPicklistLabel(service, "crmpark_option", "crmpark_bind_operation_typecode", operation.Value);
            }
        }

        protected override void ExecuteActivity(WorkflowActivityContext context, IOrganizationService service, List<string> attributeNames, DataCollection<Entity> entites)
        {
            // Получаем количество записей которые требуется обработать (если 0, то обрабатываются все записи)
            var amountRecordsToProcess = AmountRecordsToProcess.Get(context.CodeActivityContext);

            // Получаем входные параметры для привязки
            var recordId = RecordId.Get(context.CodeActivityContext);
            var entityName = EntityName.Get(context.CodeActivityContext);
            var relationName = RelationName.Get(context.CodeActivityContext);
            var operation = Operation.Get(context.CodeActivityContext);

            // Получаем основную запись, заданную через входные параметры. При её отсуствии выдаём ошибку
            Guid.TryParse(recordId, out var recordGuid);
            EntityReference target = new EntityReference(entityName, recordGuid != Guid.Empty ? recordGuid : _helper.GetIdFromUrl(recordId));
            if (string.IsNullOrEmpty(target.LogicalName) || target.Id == Guid.Empty)
            {
                throw new InvalidPluginExecutionException("Операция привязки/отвязки не может быть выполнена, так как логическое имя сущности/id записи были заданы не корректно. ");
            }

            // Проверяем существование записи в CRM
            _helper.CheckEntityExistance(service, target);

            // Попытка получения существующей связи по заданному названию. При её отсуствии выдаём ошибку
            RetrieveRelationshipResponse relationshipInfo = null;
            try
            {
                RetrieveRelationshipRequest relationshipRequest = new RetrieveRelationshipRequest()
                {
                    Name = relationName
                };
                relationshipInfo = (RetrieveRelationshipResponse)service.Execute(relationshipRequest);
            }
            catch
            {
                throw new InvalidPluginExecutionException($"Операция привязки/отвязки не может быть выполнена, так как связь под названием {relationName} не существует. ");
            }

            // Получение метаданных из полученной связи. Выдаём ошибку если связь не типа М-М
            ManyToManyRelationshipMetadata relationshipMetadata = relationshipInfo.RelationshipMetadata is ManyToManyRelationshipMetadata ? (ManyToManyRelationshipMetadata)relationshipInfo.RelationshipMetadata : throw new InvalidPluginExecutionException("Операция привязки/отвязки не может быть выполнена, так как связь между сущностями не поддерживается. ");

            // Если в результате fetch-запроса получили записи
            if (entites.Count != 0)
            {
                // Перед обработкой записей проверяем, что их сущности соответствуют сущностям связи, иначе выдаём ошибку
                var exampleEntity = entites[0];
                if (!(relationshipMetadata.Entity1LogicalName == target.LogicalName && relationshipMetadata.Entity2LogicalName == exampleEntity.LogicalName) && !(relationshipMetadata.Entity1LogicalName == exampleEntity.LogicalName && relationshipMetadata.Entity2LogicalName == target.LogicalName))
                {
                    throw new InvalidPluginExecutionException($"Операция привязки/отвязки не может быть выполнена, так как связь {relationName} не поддерживает предоставленные сущности. " +
                        $"\nУказанные в шаге сущности: {target.LogicalName} и {exampleEntity.LogicalName}. \nПоддерживаемые в связи сущности: {relationshipMetadata.Entity1LogicalName} и {relationshipMetadata.Entity2LogicalName}. ");
                }

                // Для каждой записи
                for (int i = 0; i < entites.Count && (amountRecordsToProcess == 0 || i < amountRecordsToProcess); i++)
                {
                    var currentEntityRef = entites[i].ToEntityReference();
                    try
                    {
                        // Проверяем существование связи 
                        OrganizationRequest organizationRequest = null;
                        var isRelationshipExists = RelationshipExists(service, relationshipMetadata, target, currentEntityRef);

                        // В зависимости от переданной в шаг типа привязки/отвязки
                        switch (operation.Value)
                        {
                            case (int)BindType.Bind:
                                AssociateRequest associateRequest = new AssociateRequest();
                                if (!isRelationshipExists)
                                {
                                    // Если связи не существует, формируем запрос на её создание
                                    associateRequest = new AssociateRequest()
                                    {
                                        Target = target,
                                        Relationship = new Relationship(relationName),
                                        RelatedEntities = new EntityReferenceCollection(new List<EntityReference>() { currentEntityRef })
                                    };
                                }
                                else
                                {
                                    // Если связь существует, пропускаем
                                    associateRequest = null;
                                    throw new InvalidPluginExecutionException("Указанная связь уже существует");
                                }

                                organizationRequest = associateRequest;
                                break;

                            case (int)BindType.Unbind:
                                DisassociateRequest disassociateRequest = new DisassociateRequest();
                                if (!isRelationshipExists)
                                {
                                    // Если связи не существует, пропускаем
                                    disassociateRequest = null;
                                    throw new InvalidPluginExecutionException("Указанная связь уже не существует");
                                }
                                else
                                {
                                    // Если связь существует, формируем запрос на её удаление
                                    disassociateRequest = new DisassociateRequest()
                                    {
                                        Target = target,
                                        Relationship = new Relationship(relationName),
                                        RelatedEntities = new EntityReferenceCollection(new List<EntityReference>() { currentEntityRef })
                                    };
                                }

                                organizationRequest = disassociateRequest;
                                break;

                            case (int)BindType.AddToMarketingList:
                                AddListMembersListRequest addMembersListRequest = new AddListMembersListRequest();
                                if (!isRelationshipExists)
                                {
                                    // Если связи не существует, формируем запрос на добавление в список
                                    addMembersListRequest = new AddListMembersListRequest()
                                    {
                                        ListId = target.LogicalName == "list" ? target.Id : currentEntityRef.Id,
                                        MemberIds = new Guid[1]
                                        {
                                              target.LogicalName == "list" ? currentEntityRef.Id : target.Id
                                        }
                                    };
                                }
                                else
                                {
                                    // Если связь существует, пропускаем
                                    addMembersListRequest = null;
                                    throw new InvalidPluginExecutionException("Указанная связь уже существует");
                                }

                                organizationRequest = addMembersListRequest;
                                break;

                            case (int)BindType.RemoveFromMarketingList:
                                RemoveMemberListRequest removeMembersListRequest = new RemoveMemberListRequest();
                                if (!isRelationshipExists)
                                {
                                    // Если связи не существует, пропускаем
                                    removeMembersListRequest = null;
                                    throw new InvalidPluginExecutionException("Указанная связь уже не существует");
                                }
                                else
                                {
                                    // Если связь существует, формируем запрос на удаление из списка
                                    removeMembersListRequest = new RemoveMemberListRequest()
                                    {
                                        ListId = target.LogicalName == "list" ? target.Id : currentEntityRef.Id,
                                        EntityId = target.LogicalName == "list" ? currentEntityRef.Id : target.Id,
                                    };
                                }

                                organizationRequest = removeMembersListRequest;
                                break;
                        }

                        // Если сформировали запрос
                        if (organizationRequest != null)
                        {
                            // Отправляем запрос
                            service.Execute(organizationRequest);
                            _processedAmount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Добавление возникших ошибок по каждой записи из запроса 
                        _externalErrors.AppendLine(string.Format("{0} - {1}", currentEntityRef.Id.ToString(), ex.Message));
                    }
                }
            }

            // Фиксируем кол-во успешно обработанных записей
            ProcessedRecordsQuantity.Set(context.CodeActivityContext, _processedAmount);
        }

        /// <summary>
        /// Метод проверки существования связи в CRM
        /// </summary>
        /// <param name="service">Сервис CRM</param>
        /// <param name="relationshipMetadata">Метаданные связи</param>
        /// <param name="target">Первый участник связи</param>
        /// <param name="referenced">Второй участник связи</param>
        /// <returns></returns>
        /// <exception cref="InvalidPluginExecutionException"></exception>
        private bool RelationshipExists(IOrganizationService service, ManyToManyRelationshipMetadata relationshipMetadata, EntityReference target, EntityReference referenced)
        {
            // Формируем запрос для проверки существования связи
            QueryExpression queryExpression = new QueryExpression(target.LogicalName)
            {
                ColumnSet = new ColumnSet(false)
            };

            // В зависимости от расположения участников связи определяем их порядок в ней
            if (relationshipMetadata.Entity1LogicalName == target.LogicalName)
            {
                LinkEntity linkEntity = queryExpression.AddLink(relationshipMetadata.IntersectEntityName, relationshipMetadata.Entity1IntersectAttribute, relationshipMetadata.Entity1IntersectAttribute);
                linkEntity.LinkCriteria.AddCondition(relationshipMetadata.Entity1IntersectAttribute, 0, new object[1]{ target.Id });
                linkEntity.LinkCriteria.AddCondition(relationshipMetadata.Entity2IntersectAttribute, 0, new object[1]{ referenced.Id });
            }
            else
            {
                LinkEntity linkEntity = queryExpression.AddLink(relationshipMetadata.IntersectEntityName, relationshipMetadata.Entity2IntersectAttribute, relationshipMetadata.Entity2IntersectAttribute);
                linkEntity.LinkCriteria.AddCondition(relationshipMetadata.Entity1IntersectAttribute, 0, new object[1]{ referenced.Id });
                linkEntity.LinkCriteria.AddCondition(relationshipMetadata.Entity2IntersectAttribute, 0, new object[1]{ target.Id });
            }

            // Отправляем запрос 
            RetrieveMultipleRequest retrieveMultipleRequest = new RetrieveMultipleRequest()
            {
                Query = queryExpression
            };
            RetrieveMultipleResponse multipleResponse = (RetrieveMultipleResponse)service.Execute(retrieveMultipleRequest);

            // Если запрос вернул результат, значит связь существует в CRM
            return multipleResponse.EntityCollection != null && multipleResponse.EntityCollection.Entities != null && multipleResponse.EntityCollection.Entities.Count != 0;
        }

        /// <summary>
        /// Метод указания входных значений в текст результата
        /// </summary>
        /// <param name="context">Контекст CRM</param>
        protected override void GetInputArguments(WorkflowActivityContext context)
        {
            // Формируем строку с меткой
            var operationString = string.IsNullOrEmpty(_operationLabel) ? $"{Operation.Get(context.CodeActivityContext)?.Value}" : _operationLabel + $"({Operation.Get(context.CodeActivityContext)?.Value})";

            // Получаем наследуемую строку 
            base.GetInputArguments(context);
            StringBuilder inputArguments = new StringBuilder();
            inputArguments.AppendLine($"Record URL/GUID: {RecordId.Get(context.CodeActivityContext)}");
            inputArguments.AppendLine($"Entity Name: {EntityName.Get(context.CodeActivityContext)}");
            inputArguments.AppendLine($"Relation Name: {RelationName.Get(context.CodeActivityContext)}");
            inputArguments.AppendLine($"Operation: {operationString}");
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
            outputArguments.AppendLine($"Возникшие в процессе привязки/отвязки записей ошибки:\n{_externalErrors}");
            _resultText.AppendLine(outputArguments.ToString());
        }
    }
}
