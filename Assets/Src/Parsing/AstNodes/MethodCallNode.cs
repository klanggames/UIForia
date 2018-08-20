﻿using System;
using System.Reflection;

namespace Src {

    public class MethodCallNode : ExpressionNode {

        private const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public readonly IdentifierNode identifierNode;
        public readonly MethodSignatureNode signatureNode;

        public MethodCallNode(IdentifierNode identifierNode, MethodSignatureNode signatureNode) : base(ExpressionNodeType.MethodCall) {
            this.identifierNode = identifierNode;
            this.signatureNode = signatureNode;
        }

        public override Type GetYieldedType(ContextDefinition context) {
            MethodInfo info;
            if (signatureNode.parts.Count == 0) {
                info = context.processedType.rawType.GetMethod(identifierNode.identifier, flags);
                if (info == null) {
                    throw new Exception("Method missing");
                }
                return info.ReturnType;
            }
            Type[] types = new Type[signatureNode.parts.Count];
            for (int i = 0; i < types.Length; i++) {
                types[i] = signatureNode.parts[i].GetYieldedType(context);
            }
            info = context.processedType.rawType.GetMethod(identifierNode.identifier, flags, null, types, null);
            if (info == null) {
                throw new Exception("Method missing");
            }
            return info.ReturnType;
        }

    }

}