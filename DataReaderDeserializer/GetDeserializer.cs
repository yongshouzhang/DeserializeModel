using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using System.Reflection;
using System.Data;
namespace DataReaderDeserializer
{
    public static class GetDeserializer<T>
    {
        public static T ReflectDeserialize(IDataReader reader)
        {
            DbFieldReader dbReader = new DbFieldReader(reader);
            var columnList = Enumerable.Range(0, reader.FieldCount).Select(ordinal =>
            {
                Type fieldType = reader.GetFieldType(ordinal);
                return new
                {
                    Name = reader.GetName(ordinal),
                    Index = ordinal,
                    Type = fieldType,
                };
            });

            T model = Activator.CreateInstance<T>();
             typeof(T).GetProperties().ToList().ForEach(prop =>
            {
                var column = columnList.FirstOrDefault(obj => obj.Type == prop.PropertyType
                && string.Equals(prop.Name, obj.Name, StringComparison.CurrentCultureIgnoreCase));

                if (column == null)
                {
                    prop.SetValue(model, TypeHelper.GetDefault(prop.PropertyType), null);
                }else
                {
                    prop.SetValue(model, FieldReader.GetReaderMethod(prop.PropertyType).Invoke(dbReader, new object[] { column.Index }), null);
                }
            });
            return model;
        }
        public static Func<IDataReader,T> Deserialize(IDataReader reader)
        {

            ParameterExpression readerParam = Expression.Parameter(typeof(FieldReader), "reader");
            ParameterExpression outerReaderParam = Expression.Parameter(typeof(IDataReader), "outerReader");
            var columnList = Enumerable.Range(0, reader.FieldCount).Select(ordinal =>
             {
                 Type fieldType = reader.GetFieldType(ordinal);
                 return new ColumnInfo
                 {
                     Type = fieldType,
                     Name = reader.GetName(ordinal),
                     Index = ordinal,
                     Reader = Expression.Call(readerParam, FieldReader.GetReaderMethod(fieldType), new Expression[] { Expression.Constant(ordinal) })
                 };
             });

            var propList = typeof(T).GetProperties().Select(prop =>
             {
                 var column = columnList.FirstOrDefault(obj => obj.Type == prop.PropertyType
                 && string.Equals(prop.Name, obj.Name, StringComparison.CurrentCultureIgnoreCase));

                 if (column == null)
                 {
                     return new TPropertyInfo
                     {
                         Info = prop,
                         Expression = Expression.Call(readerParam, typeof(FieldReader).GetMethod("GetDefault"), new[] { Expression.Constant(prop.PropertyType) })
                     };
                 }
                 return new TPropertyInfo
                 {
                     Info = prop,
                     Expression = column.Reader
                 };
             });

            ConstructorInfo[] constructorList = typeof(T).GetConstructors();

            Expression lambdaBody;
            if (constructorList.Any(ctor => ctor.GetParameters().Count() == 0))
            {
                lambdaBody = Expression.MemberInit(Expression.New(typeof(T)),
                     propList.Select(obj => Expression.Bind(obj.Info as MemberInfo, obj.Expression) as MemberBinding));
            }else
            {
                ConstructorInfo constructor = constructorList.FirstOrDefault(obj => obj.GetParameters().Count() == propList.Count());
                if (constructor == null) throw new ArgumentException(" 该类型构造函数参数个数与属性不匹配");
                lambdaBody = Expression.New(constructor, propList.Select(obj => obj.Expression));
            }
            Expression<Func<IDataReader, FieldReader>> CreateDbFieldReader = (_reader) => new DbFieldReader(_reader);
            LambdaExpression innerLambda = Expression.Lambda<Func<FieldReader, T>>(lambdaBody, readerParam);
            Expression fieldReader = Expression.Invoke(CreateDbFieldReader, outerReaderParam);

            return Expression.Lambda<Func<IDataReader, T>>(Expression.Invoke(innerLambda, fieldReader), outerReaderParam).Compile();
        }
        class ColumnInfo
        {
            public int Index { get; set; }
            public string Name { get; set; }
            public Type Type { get; set; }

            public Expression Reader { get; set; }
        }
        class TPropertyInfo
        {
            public PropertyInfo Info { get; set; }
            public Expression Expression { get; set; }
        }
    }
}
