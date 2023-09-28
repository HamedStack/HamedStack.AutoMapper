// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global

using System.Collections.ObjectModel;
using System.Data;
using System.Dynamic;
using System.Linq.Expressions;
using AutoMapper;

namespace HamedStack.AutoMapper
{
    public static class AutoMapperExtensions
    {
        public static async Task<TDestination> AfterMapAsync<TSource, TDestination>(
            this IMapper mapper, TSource source, Func<TSource, TDestination, Task> asyncAction)
        {
            var destination = mapper.Map<TSource, TDestination>(source);
            await asyncAction(source, destination);
            return destination;
        }

        public static IMappingExpression<TSource, TDestination> AppendStringProperty<TSource, TDestination>(
            this IMappingExpression<TSource, TDestination> expression,
            Expression<Func<TDestination, string>> destinationSelector,
            Expression<Func<TSource, string>> sourceSelector,
            string suffix)
        {
            var compiledSourceSelector = sourceSelector.Compile();
            return expression.ForMember(destinationSelector, opt => opt.MapFrom(src => compiledSourceSelector(src) + suffix));
        }

        public static T Clone<T>(this IMapper mapper, T source)
        {
            return mapper.Map<T, T>(source);
        }

        public static TDestination? ConditionalMap<TSource, TDestination>(
            this IMapper mapper, TSource source, Func<TSource, bool> predicate)
        {
            return predicate(source) ? mapper.Map<TSource, TDestination>(source) : default;
        }

        public static IMappingExpression<TSource, TDestination> Default<TSource, TDestination, TPropertyType>(
            this IMappingExpression<TSource, TDestination> expression,
            Expression<Func<TDestination, TPropertyType>> destSelector,
            TPropertyType defaultValue)
        {
            var property = ((MemberExpression)destSelector.Body).Member.Name;
            return expression.ForMember(destSelector, opt => opt.MapFrom((src, _) =>
            {
                var val = src?.GetType().GetProperty(property)?.GetValue(src, null);
                return val ?? defaultValue;
            }));
        }

        public static TDestination EnsureValidMap<TSource, TDestination>(
            this IMapper mapper, TSource source)
        {
            mapper.ConfigurationProvider.AssertConfigurationIsValid();
            return mapper.Map<TSource, TDestination>(source);
        }

        public static IEnumerable<TDestination> EnumerateAndMap<TSource, TDestination>(
            this IMapper mapper, IEnumerable<TSource> source)
        {
            foreach (var item in source)
            {
                yield return mapper.Map<TSource, TDestination>(item);
            }
        }

        public static IMappingExpression<TSource, TDestination> ExcludeProperties<TSource, TDestination>(
            this IMappingExpression<TSource, TDestination> mappingExpression, params string[] propertiesToExclude)
        {
            foreach (var property in propertiesToExclude)
            {
                mappingExpression.ForMember(property, opt => opt.Ignore());
            }
            return mappingExpression;
        }

        public static TDestination FallbackMap<TSource, TDestination>(
            this IMapper primaryMapper, TSource source, IMapper fallbackMapper)
        {
            try
            {
                return primaryMapper.Map<TSource, TDestination>(source);
            }
            catch
            {
                return fallbackMapper.Map<TSource, TDestination>(source);
            }
        }

        public static IMappingExpression<TSource, TDestination> IgnoreNonExisting<TSource, TDestination>(
            this IMappingExpression<TSource, TDestination> expression)
        {
            var sourceProperties = typeof(TSource).GetProperties().Select(x => x.Name).ToList();
            var destinationProperties = typeof(TDestination).GetProperties();

            foreach (var property in destinationProperties)
            {
                if (!sourceProperties.Contains(property.Name))
                {
                    expression.ForMember(property.Name, opt => opt.Ignore());
                }
            }

            return expression;
        }

        public static TDestination IgnoreUnmapped<TSource, TDestination>(
            this IMapper mapper, TSource source) where TDestination : new()
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<TSource, TDestination>().ForAllMembers(opt => opt.Ignore());
            });
            var ignoringMapper = config.CreateMapper();
            return ignoringMapper.Map(source, new TDestination());
        }

        public static IMappingExpression<TSource, TDestination> IncludeBaseMappings<TSource, TDestination, TBaseSource, TBaseDestination>(
            this IMappingExpression<TSource, TDestination> expression)
        {
            return expression.IncludeBase<TBaseSource, TBaseDestination>();
        }

        public static TDestination MapByKey<TSource, TDestination>(
            this IMapper mapper, TSource source, string key)
        {
            var destination = Activator.CreateInstance<TDestination>();
            var sourceProperties = typeof(TSource).GetProperties().Where(p => p.Name.Contains(key)).ToList();
            var destProperties = typeof(TDestination).GetProperties().ToList();

            foreach (var sourceProp in sourceProperties)
            {
                var destProp = destProperties.FirstOrDefault(p => p.Name == sourceProp.Name.Replace(key, ""));
                if (destProp != null)
                {
                    var value = sourceProp.GetValue(source);
                    destProp.SetValue(destination, value);
                }
            }
            return destination;
        }

        public static void MapCollectionBy<TSource, TDestination, TKey>(
            this IMapper mapper,
            IEnumerable<TSource> source,
            ICollection<TDestination> destination,
            Func<TSource, TKey> sourceKey,
            Func<TDestination, TKey> destinationKey)
        {
            foreach (var sourceItem in source)
            {
                var destItem = destination.FirstOrDefault(d => destinationKey(d)?.Equals(sourceKey(sourceItem)) ?? false);
                if (destItem != null)
                {
                    mapper.Map(sourceItem, destItem);
                }
                else
                {
                    destination.Add(mapper.Map<TDestination>(sourceItem));
                }
            }
        }

        public static IEnumerable<TDestination> MapDistinct<TSource, TDestination, TKey>(
            this IMapper mapper, IEnumerable<TSource> source, Func<TDestination, TKey> keySelector)
        {
            return mapper.Map<IEnumerable<TSource>, IEnumerable<TDestination>>(source).DistinctBy(keySelector);
        }

        public static TDestination MapEnumerableProperty<TSource, TDestination, TProperty>(
            this IMapper mapper, TSource source, Expression<Func<TDestination, IEnumerable<TProperty>>> propertySelector)
        {
            var destination = mapper.Map<TSource, TDestination>(source);
            var property = destination?.GetType().GetProperty(((MemberExpression)propertySelector.Body).Member.Name);
            if (property != null)
            {
                if (property.GetValue(destination) is not IEnumerable<TProperty> propertyValue) return destination;
                property.SetValue(destination, propertyValue.ToList());
            }
            return destination;
        }

        public static TDestination MapFlattened<TSource, TDestination>(
            this IMapper mapper, TSource source)
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<TSource, TDestination>().MaxDepth(1);
            });
            var localMapper = config.CreateMapper();
            return localMapper.Map<TSource, TDestination>(source);
        }

        public static TDestination? MapIf<TSource, TDestination>(
            this IMapper mapper, TSource source, Func<TSource, bool> condition)
        {
            if (condition(source))
            {
                return mapper.Map<TSource, TDestination>(source);
            }
            return default;
        }

        public static TDestination MapIfChanged<TSource, TDestination>(
            this IMapper mapper, TSource source, TDestination destination)
        {
            var originalDestination = mapper.Map<TDestination, TDestination>(destination);
            var newDestination = mapper.Map(source, destination);
            foreach (var prop in typeof(TDestination).GetProperties())
            {
                if (Equals(prop.GetValue(originalDestination), prop.GetValue(newDestination)))
                {
                    prop.SetValue(newDestination, prop.GetValue(originalDestination));
                }
            }
            return newDestination;
        }

        public static TDestination MapIfExists<TSource, TDestination>(
            this IMapper mapper, TSource source)
        {
            var destinationProperties = typeof(TDestination).GetProperties().Select(p => p.Name);
            var sourceProperties = typeof(TSource).GetProperties().Select(p => p.Name);

            var existingProperties = destinationProperties.Intersect(sourceProperties).ToList();

            var destination = mapper.Map<TSource, TDestination>(source);
            var props = destination?.GetType().GetProperties();
            if (props != null)
                foreach (var property in props)
                {
                    if (!existingProperties.Contains(property.Name))
                    {
                        property.SetValue(destination, null);
                    }
                }

            return destination;
        }

        public static TDestination? MapIfNotNull<TSource, TDestination>(
            this IMapper mapper, TSource source)
        {
            return source == null ? default : mapper.Map<TSource, TDestination>(source);
        }

        public static Dictionary<TKey, TDestination> MapListToDictionary<TSource, TDestination, TKey>(
            this IMapper mapper, IEnumerable<TSource> source, Func<TDestination, TKey> keySelector) where TKey : notnull
        {
            var mapped = mapper.Map<IEnumerable<TSource>, List<TDestination>>(source);
            return mapped.ToDictionary(keySelector);
        }

        public static TDestination MapPartial<TSource, TDestination>(
            this IMapper mapper, TSource source, params Expression<Func<TSource, object>>[] properties)
        {
            var config = new MapperConfiguration(cfg =>
            {
                var map = cfg.CreateMap<TSource, TDestination>();
                foreach (var prop in typeof(TDestination).GetProperties())
                {
                    if (properties.All(p => ((MemberExpression)p.Body).Member.Name != prop.Name))
                    {
                        map.ForMember(prop.Name, opt => opt.Ignore());
                    }
                }
            });
            var partialMapper = config.CreateMapper();
            return partialMapper.Map<TSource, TDestination>(source);
        }

        public static DataTable MapToDataTable<TSource>(
            this IMapper mapper, IEnumerable<TSource> source)
        {
            var table = new DataTable();
            var enumerable = source.ToList();
            if (!enumerable.Any()) return table;

            var firstRecord = mapper.Map<Dictionary<string, object>>(enumerable.First());
            foreach (var key in firstRecord.Keys)
            {
                table.Columns.Add(new DataColumn(key, firstRecord[key].GetType()));
            }

            foreach (var record in enumerable)
            {
                var mappedRecord = mapper.Map<Dictionary<string, object>>(record);
                table.Rows.Add(mappedRecord.Values.ToArray());
            }

            return table;
        }

        public static Dictionary<string, object> MapToDictionary<TSource>(
            this IMapper mapper, TSource source)
        {
            return mapper.Map<TSource, Dictionary<string, object>>(source);
        }

        public static dynamic MapToDynamic<TSource>(
            this IMapper mapper, TSource source)
        {
            var expando = new ExpandoObject();
            var dictionary = expando as IDictionary<string, object?>; // ExpandoObject supports IDictionary<string, object>

            foreach (var property in typeof(TSource).GetProperties())
            {
                dictionary[property.Name] = property.GetValue(source);
            }

            return expando;
        }

        public static HashSet<TDestination> MapToHashSet<TSource, TDestination>(
            this IMapper mapper, IEnumerable<TSource> source)
        {
            return mapper.Map<IEnumerable<TSource>, HashSet<TDestination>>(source);
        }

        public static List<TDestination> MapToList<TSource, TDestination>(
            this IMapper mapper, IEnumerable<TSource> source)
        {
            return mapper.Map<IEnumerable<TSource>, List<TDestination>>(source);
        }

        public static T MapToNew<T>(this IMapper mapper, object source)
        {
            return mapper.Map<T>(source);
        }
        public static ObservableCollection<TDestination> MapToObservableCollection<TSource, TDestination>(
            this IMapper mapper, IEnumerable<TSource> source)
        {
            var collection = mapper.Map<IEnumerable<TSource>, ObservableCollection<TDestination>>(source);
            return collection;
        }

        public static (TDestination1, TDestination2) MapToTuple<TSource, TDestination1, TDestination2>(
            this IMapper mapper, TSource source)
        {
            return (mapper.Map<TSource, TDestination1>(source), mapper.Map<TSource, TDestination2>(source));
        }

        public static void MapToUpdate<T>(this IMapper mapper, T source, T destination)
        {
            mapper.Map(source, destination);
        }
        public static TDestination MapWithAction<TSource, TDestination>(
            this IMapper mapper, TSource source, Action<TSource, TDestination> postMapAction)
        {
            var destination = mapper.Map<TSource, TDestination>(source);
            postMapAction(source, destination);
            return destination;
        }

        public static IMappingExpression<TSource, TDestination> PrependStringProperty<TSource, TDestination>(
            this IMappingExpression<TSource, TDestination> expression,
            Expression<Func<TDestination, string>> destinationSelector,
            Expression<Func<TSource, string>> sourceSelector,
            string prefix)
        {
            var compiledSourceSelector = sourceSelector.Compile();
            return expression.ForMember(destinationSelector, opt => opt.MapFrom(src => prefix + compiledSourceSelector(src)));
        }

        public static TDestination? ProjectToFirstOrDefault<TSource, TDestination>(
            this IMapper mapper, IQueryable<TSource> source)
        {
            return mapper.ProjectTo<TDestination>(source).FirstOrDefault();
        }

        public static TDestination? SafeMap<TSource, TDestination>(
            this IMapper mapper, TSource source)
        {
            try
            {
                return mapper.Map<TSource, TDestination>(source);
            }
            catch
            {
                return default;
            }
        }

        public static TDestination? SkipIfDefault<TSource, TDestination>(
            this IMapper mapper, TSource source)
        {
            if (EqualityComparer<TSource>.Default.Equals(source, default))
            {
                return default;
            }
            return mapper.Map<TSource, TDestination>(source);
        }

        public static TDestination? SkipMapping<TSource, TDestination>(
            this IMapper mapper, TSource source, Func<TSource, bool> predicate)
        {
            return predicate(source) ? default : mapper.Map<TSource, TDestination>(source);
        }

        public static Dictionary<TKey, List<TDestination>> ToGroupedDictionary<TSource, TDestination, TKey>(
            this IMapper mapper, IEnumerable<TSource> source, Func<TDestination, TKey> keySelector) where TKey : notnull
        {
            var mapped = mapper.Map<IEnumerable<TSource>, List<TDestination>>(source);
            return mapped.GroupBy(keySelector).ToDictionary(g => g.Key, g => g.ToList());
        }

        public static TDestination TransformProperty<TSource, TDestination>(
            this IMapper mapper, TSource source, string propertyName, Func<object?, object?> transformFunc)
        {
            var destination = mapper.Map<TSource, TDestination>(source);
            var property = destination?.GetType().GetProperty(propertyName);
            if (property != null)
            {
                var value = property.GetValue(destination);
                property.SetValue(destination, transformFunc(value));
            }
            return destination;
        }

        public static ICollection<TDestination> UpdateCollection<TSource, TDestination>(
            this IMapper mapper, IEnumerable<TSource> source, ICollection<TDestination> destination)
        {
            destination.Clear();
            foreach (var item in source)
            {
                destination.Add(mapper.Map<TSource, TDestination>(item));
            }
            return destination;
        }

        public static IMappingExpression<TSource, TDestination> UseDestinationIfSourceNull<TSource, TDestination>(
                                                                                    this IMappingExpression<TSource, TDestination> expression,
            Expression<Func<TSource, object>> sourceMember,
            Expression<Func<TDestination, object>> destMember)
        {
            return expression.ForMember(destMember, opt => opt.MapFrom(src => sourceMember.Compile()(src)));
        }
    }
}
