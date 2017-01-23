﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Waher.Persistence;
using Waher.Persistence.Filters;
using Waher.Script;
using Waher.Script.Abstraction;
using Waher.Script.Abstraction.Elements;
using Waher.Script.Exceptions;
using Waher.Script.Model;
using Waher.Script.Objects.VectorSpaces;
using Waher.Script.Operators.Comparisons;
using Waher.Script.Operators.Logical;

namespace Waher.Script.Persistence.Functions
{
	/// <summary>
	/// Finds object in the object database.
	/// </summary>
	public class Find : FunctionMultiVariate
	{
		private static MethodInfo findMethodGeneric = GetFindMethod();

		/// <summary>
		/// Finds object in the object database.
		/// </summary>
		/// <param name="Size">Size</param>
		/// <param name="Start">Start position in script expression.</param>
		/// <param name="Length">Length of expression covered by node.</param>
		public Find(ScriptNode Type, ScriptNode Offset, ScriptNode MaxCount, ScriptNode Filter, ScriptNode SortOrder, int Start, int Length, Expression Expression)
			: base(new ScriptNode[] { Type, Offset, MaxCount, Filter, SortOrder }, 
				  new ArgumentType[] { ArgumentType.Scalar, ArgumentType.Scalar, ArgumentType.Scalar, ArgumentType.Scalar, ArgumentType.Vector }, 
				  Start, Length, Expression)
		{
		}

		private static MethodInfo GetFindMethod()
		{
			Type T = typeof(Database);
			return T.GetMethod("Find", new Type[] { typeof(int), typeof(int), typeof(Filter), typeof(string[]) });
		}

		/// <summary>
		/// Default Argument names
		/// </summary>
		public override string[] DefaultArgumentNames
		{
			get
			{
				return new string[]
				{
					"Type",
					"Offset",
					"MaxCount",
					"Filter",
					"SortOrder"
				};
			}
		}

		/// <summary>
		/// Name of the function
		/// </summary>
		public override string FunctionName
		{
			get
			{
				return "find";
			}
		}

		/// <summary>
		/// Evaluates the function.
		/// </summary>
		/// <param name="Arguments">Function arguments.</param>
		/// <param name="Variables">Variables collection.</param>
		/// <returns>Function result.</returns>
		public override IElement Evaluate(IElement[] Arguments, Variables Variables)
		{
			Type T = Arguments[0].AssociatedObjectValue as Type;
			if (T == null)
				throw new ScriptRuntimeException("First parameter must be a type.", this);

			int Offset = (int)Expression.ToDouble(Arguments[1].AssociatedObjectValue);
			int MaxCount = (int)Expression.ToDouble(Arguments[2].AssociatedObjectValue);
			object FilterObj = Arguments[3].AssociatedObjectValue;
			Filter Filter = FilterObj as Filter;
			IVector V = Arguments[4] as IVector;
			int i, c = V.Dimension;
			string[] SortOrder = new string[c];

			for (i = 0; i < c; i++)
				SortOrder[i] = V.GetElement(i).AssociatedObjectValue.ToString();

			if (Filter == null && FilterObj != null)
			{
				Expression Exp = new Expression(FilterObj.ToString());
				Filter = this.Convert(Exp.Root, Variables);
			}

			MethodInfo MI = findMethodGeneric.MakeGenericMethod(new Type[] { T });
			object Result = MI.Invoke(null, new object[] { Offset, MaxCount, Filter, SortOrder });
			Task Task = Result as Task;
			if (Task != null)
			{
				Task.Wait();

				PropertyInfo PI = Task.GetType().GetProperty("Result");
				Result = PI.GetMethod.Invoke(Task, null);
			}

			IEnumerable E = Result as IEnumerable;
			if (E != null)
			{
				LinkedList<IElement> Elements = new LinkedList<IElement>();
				IEnumerator e = E.GetEnumerator();
				while (e.MoveNext())
					Elements.AddLast(Expression.Encapsulate(e.Current));

				return new ObjectVector(Elements);
			}
			else
				return Expression.Encapsulate(Result);
		}

		private Filter Convert(ScriptNode Node, Variables Variables)
		{
			string FieldName;
			object Value;

			if (Node is And)
			{
				And And = (And)Node;
				return new FilterAnd(this.Convert(And.LeftOperand, Variables), this.Convert(And.RightOperand, Variables));
			}
			else if (Node is Or)
			{
				Or Or = (Or)Node;
				return new FilterOr(this.Convert(Or.LeftOperand, Variables), this.Convert(Or.RightOperand, Variables));
			}
			else if (Node is Not)
			{
				Not Not = (Not)Node;
				return new FilterNot(this.Convert(Not.Operand, Variables));
			}
			else if (Node is EqualTo)
			{
				this.CheckBinaryOperator((BinaryOperator)Node, Variables, out FieldName, out Value);
				return new FilterFieldEqualTo(FieldName, Value);
			}
			else if (Node is NotEqualTo)
			{
				this.CheckBinaryOperator((BinaryOperator)Node, Variables, out FieldName, out Value);
				return new FilterFieldNotEqualTo(FieldName, Value);
			}
			else if (Node is LesserThan)
			{
				this.CheckBinaryOperator((BinaryOperator)Node, Variables, out FieldName, out Value);
				return new FilterFieldLesserThan(FieldName, Value);
			}
			else if (Node is GreaterThan)
			{
				this.CheckBinaryOperator((BinaryOperator)Node, Variables, out FieldName, out Value);
				return new FilterFieldGreaterThan(FieldName, Value);
			}
			else if (Node is LesserThanOrEqualTo)
			{
				this.CheckBinaryOperator((BinaryOperator)Node, Variables, out FieldName, out Value);
				return new FilterFieldLesserOrEqualTo(FieldName, Value);
			}
			else if (Node is GreaterThanOrEqualTo)
			{
				this.CheckBinaryOperator((BinaryOperator)Node, Variables, out FieldName, out Value);
				return new FilterFieldGreaterOrEqualTo(FieldName, Value);
			}
			else if (Node is Like)
			{
				this.CheckBinaryOperator((BinaryOperator)Node, Variables, out FieldName, out Value);
				return new FilterFieldLikeRegEx(FieldName, Value.ToString());
			}
			else if (Node is NotLike)
			{
				this.CheckBinaryOperator((BinaryOperator)Node, Variables, out FieldName, out Value);
				return new FilterNot(new FilterFieldLikeRegEx(FieldName, Value.ToString()));
			}
			else
				throw new ScriptRuntimeException("Invalid operation for filters.", this);
		}

		private void CheckBinaryOperator(BinaryOperator Operator, Variables Variables, out string FieldName, out object Value)
		{
			VariableReference v = Operator.LeftOperand as VariableReference;
			if (v == null)
				throw new ScriptRuntimeException("Left operands in binary filter operators need to be a variable references, as they refer to field names.", this);

			FieldName = v.VariableName;
			Value = Operator.RightOperand.Evaluate(Variables).AssociatedObjectValue;
		}

	}
}
