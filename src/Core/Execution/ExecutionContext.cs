using System;
using System.Collections.Generic;
using HotChocolate.Configuration;
using HotChocolate.Language;
using HotChocolate.Resolvers;
using HotChocolate.Runtime;
using HotChocolate.Types;

namespace HotChocolate.Execution
{
    internal class ExecutionContext
        : IExecutionContext
    {
        private readonly List<IQueryError> _errors = new List<IQueryError>();
        private readonly ServiceFactory _serviceFactory = new ServiceFactory();
        private readonly FieldCollector _fieldCollector;
        private readonly DirectiveCollector _directiveCollector;
        private readonly ISession _session;
        private readonly IResolverCache _resolverCache;
        private readonly bool _disposeRootValue;

        private bool _disposed;

        public ExecutionContext(
            ISchema schema,
            DocumentNode queryDocument,
            OperationDefinitionNode operation,
            OperationRequest request,
            VariableCollection variables)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            Schema = schema
                ?? throw new ArgumentNullException(nameof(schema));
            QueryDocument = queryDocument
                ?? throw new ArgumentNullException(nameof(queryDocument));
            Operation = operation
                ?? throw new ArgumentNullException(nameof(operation));
            Variables = variables
                ?? throw new ArgumentNullException(nameof(variables));

            Services = _serviceFactory.Services = request.Services;
            _session = request.Session;
            _resolverCache = request.Session?.CustomContexts
                .GetCustomContext<IResolverCache>();

            Fragments = new FragmentCollection(schema, queryDocument);
            _fieldCollector = new FieldCollector(variables, Fragments);
            _directiveCollector = new DirectiveCollector(schema);
            OperationType = schema.GetOperationType(operation.Operation);
            RootValue = ResolveRootValue(request.Services, schema,
                OperationType, request.InitialValue);

            if (RootValue == null)
            {
                RootValue = CreateRootValue(Services, schema, OperationType);
                _disposeRootValue = true;
            }
        }

        public ISchema Schema { get; }

        public IReadOnlySchemaOptions Options => Schema.Options;

        public IServiceProvider Services { get; }

        public object RootValue { get; }

        public IDataLoaderProvider DataLoaders => _session.DataLoaders;

        public ICustomContextProvider CustomContexts => _session.CustomContexts;

        public DocumentNode QueryDocument { get; }

        public OperationDefinitionNode Operation { get; }

        public ObjectType OperationType { get; }

        public FragmentCollection Fragments { get; }

        public VariableCollection Variables { get; }

        public void ReportError(IQueryError error)
        {
            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            _errors.Add(error);
        }

        public IEnumerable<IQueryError> GetErrors() => _errors;

        public IReadOnlyCollection<FieldSelection> CollectFields(
            ObjectType objectType, SelectionSetNode selectionSet)
        {
            if (objectType == null)
            {
                throw new ArgumentNullException(nameof(objectType));
            }

            if (selectionSet == null)
            {
                throw new ArgumentNullException(nameof(selectionSet));
            }

            return _fieldCollector.CollectFields(
                objectType, selectionSet, ReportError);
        }

        public IReadOnlyCollection<IDirective> CollectDirectives(
            ObjectType objectType,
            FieldSelection fieldSelection,
            DirectiveScope scope)
        {
            return _directiveCollector.CollectDirectives(
                objectType, fieldSelection.Field,
                fieldSelection.Selection, scope);
        }

        public T GetResolver<T>()
        {
            if (_resolverCache == null)
            {
                throw new NotSupportedException(
                    "The resolver cache is disabled and resolver types are " +
                    "not supported in the current schema.");
            }

            if (!_resolverCache.TryGetResolver<T>(out T resolver))
            {
                if (Services.GetService(typeof(T)) is T res)
                {
                    resolver = res;
                }
                else
                {
                    resolver = _resolverCache.AddOrGetResolver(
                        () => CreateResolver<T>());
                }
            }

            return resolver;
        }

        private static object ResolveRootValue(
            IServiceProvider services,
            ISchema schema,
            ObjectType operationType,
            object initialValue)
        {
            if (initialValue == null && schema.TryGetNativeType(
               operationType.Name, out Type nativeType))
            {
                initialValue = services.GetService(nativeType);
            }
            return initialValue;
        }

        private static object CreateRootValue(
            IServiceProvider services,
            ISchema schema,
            ObjectType operationType)
        {
            if (schema.TryGetNativeType(
                operationType.Name,
                out Type nativeType))
            {
                ServiceFactory serviceFactory = new ServiceFactory();
                serviceFactory.Services = services;
                return serviceFactory.CreateInstance(nativeType);
            }
            return null;
        }

        private T CreateResolver<T>() =>
            (T)_serviceFactory.CreateInstance(typeof(T));

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                if (_disposeRootValue && RootValue is IDisposable d)
                {
                    d.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
