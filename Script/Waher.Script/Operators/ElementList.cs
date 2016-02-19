﻿using System;
using System.Collections.Generic;
using System.Text;
using Waher.Script.Abstraction.Elements;
using Waher.Script.Model;
using Waher.Script.Operators.Vectors;

namespace Waher.Script.Operators
{
    /// <summary>
    /// Represents a list of elements.
    /// </summary>
    public class ElementList : ScriptNode
    {
        ScriptNode[] elements;

        /// <summary>
        /// Represents a list of elements.
        /// </summary>
        /// <param name="Elements">Elements.</param>
        /// <param name="Start">Start position in script expression.</param>
        /// <param name="Length">Length of expression covered by node.</param>
        public ElementList(ScriptNode[] Elements, int Start, int Length)
            : base(Start, Length)
        {
            this.elements = Elements;
        }

        /// <summary>
        /// Elements.
        /// </summary>
        public ScriptNode[] Elements
        {
            get { return this.elements; }
        }

        /// <summary>
        /// Evaluates the node, using the variables provided in the <paramref name="Variables"/> collection.
        /// </summary>
        /// <param name="Variables">Variables collection.</param>
        /// <returns>Result.</returns>
        public override IElement Evaluate(Variables Variables)
        {
            LinkedList<IElement> List = new LinkedList<IElement>();

            foreach (ScriptNode E in this.elements)
            {
                if (E == null)
                    List.AddLast((IElement)null);
                else
                    List.AddLast(E.Evaluate(Variables));
            }

            return VectorDefinition.Encapsulate(List, this);
        }

    }
}
