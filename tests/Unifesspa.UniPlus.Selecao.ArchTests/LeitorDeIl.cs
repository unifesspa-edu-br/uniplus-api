namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Reflection;
using System.Reflection.Emit;

using ReflectionAssembly = System.Reflection.Assembly;

/// <summary>Lê o IL dos métodos de um assembly e devolve os métodos e construtores que cada tipo chama.</summary>
internal static class LeitorDeIl
{
    internal static IEnumerable<(System.Type Caller, MethodBase Callee)> ChamadasDoAssembly(ReflectionAssembly assembly) =>
        ChamadasPorMetodo(assembly).Select(static c => (c.Caller, c.Callee));

    internal static IEnumerable<(System.Type Caller, MethodBase Metodo, MethodBase Callee)> ChamadasPorMetodo(ReflectionAssembly assembly)
    {
        foreach (System.Type tipo in assembly.GetTypes())
        {
            foreach (MethodBase metodo in tipo.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                         .OfType<MethodBase>())
            {
                foreach (MethodBase alvo in AlvosDasChamadas(metodo))
                {
                    yield return (tipo, metodo, alvo);
                }
            }
        }
    }

    /// <summary>
    /// O método de instância escreve estado: grava um campo (<c>stfld</c>, inclusive pelo acessor de escrita)
    /// ou chama, no mesmo assembly, outro método de instância que grava. Cobre o mutador que devolve valor.
    /// </summary>
    internal static bool EscreveEstado(MethodBase metodo) => EscreveEstado(metodo, []);

    private static bool EscreveEstado(MethodBase metodo, HashSet<MethodBase> visitados)
    {
        if (metodo.IsStatic || metodo.IsConstructor || !visitados.Add(metodo))
        {
            return false;
        }

        return GravaCampo(metodo)
            || AlvosDasChamadas(metodo).Any(alvo => alvo.DeclaringType?.Assembly == metodo.DeclaringType?.Assembly && EscreveEstado(alvo, visitados));
    }

    private static bool GravaCampo(MethodBase metodo)
    {
        byte[]? il = metodo.GetMethodBody()?.GetILAsByteArray();
        for (int i = 0; il is not null && i < il.Length;)
        {
            OpCode opcode = LerOpcode(il, ref i);
            if (opcode == OpCodes.Stfld && GravaCampoDoProprioTipo(metodo, BitConverter.ToInt32(il, i)))
            {
                return true;
            }

            i += TamanhoDoOperando(opcode, il, i);
        }

        return false;
    }

    /// <summary>
    /// Campo do próprio tipo (ou de um tipo-base). Os campos de fechamentos e de máquinas de estado gerados
    /// pelo compilador também se gravam com <c>stfld</c> e não são estado da parte.
    /// </summary>
    private static bool GravaCampoDoProprioTipo(MethodBase metodo, int token)
    {
        try
        {
            FieldInfo campo = metodo.Module.ResolveField(token, metodo.DeclaringType?.GetGenericArguments(), null)!;
            return campo.DeclaringType is { } dono
                && dono.IsAssignableFrom(metodo.DeclaringType)
                && !System.Attribute.IsDefined(dono, typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static IEnumerable<MethodBase> AlvosDasChamadas(MethodBase metodo)
    {
        byte[]? il = metodo.GetMethodBody()?.GetILAsByteArray();
        if (il is null)
        {
            yield break;
        }

        System.Type[]? tiposDoTipo = metodo.DeclaringType is { IsGenericType: true } declarante ? declarante.GetGenericArguments() : null;
        System.Type[]? tiposDoMetodo = metodo.IsGenericMethod ? metodo.GetGenericArguments() : null;
        for (int i = 0; i < il.Length;)
        {
            OpCode opcode = LerOpcode(il, ref i);
            if (opcode.OperandType is OperandType.InlineMethod)
            {
                MethodBase? alvo = Resolver(metodo.Module, BitConverter.ToInt32(il, i), tiposDoTipo, tiposDoMetodo);
                if (alvo is not null)
                {
                    yield return alvo;
                }
            }

            i += TamanhoDoOperando(opcode, il, i);
        }
    }

    private static MethodBase? Resolver(Module modulo, int token, System.Type[]? tiposDoTipo, System.Type[]? tiposDoMetodo)
    {
        try
        {
            return modulo.ResolveMethod(token, tiposDoTipo, tiposDoMetodo);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static OpCode LerOpcode(byte[] il, ref int posicao)
    {
        byte primeiro = il[posicao++];
        return primeiro == 0xFE ? TabelaDeDoisBytes[il[posicao++]] : TabelaDeUmByte[primeiro];
    }

    private static int TamanhoDoOperando(OpCode opcode, byte[] il, int posicao) => opcode.OperandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + (BitConverter.ToInt32(il, posicao) * 4),
        _ => 4,
    };

    private static readonly OpCode[] TabelaDeUmByte = TabelaDeOpcodes(umByte: true);

    private static readonly OpCode[] TabelaDeDoisBytes = TabelaDeOpcodes(umByte: false);

    private static OpCode[] TabelaDeOpcodes(bool umByte)
    {
        OpCode[] tabela = new OpCode[256];
        foreach (OpCode opcode in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
                     .Select(static f => (OpCode)f.GetValue(null)!))
        {
            ushort valor = (ushort)opcode.Value;
            if (umByte && opcode.Size == 1)
            {
                tabela[valor] = opcode;
            }
            else if (!umByte && opcode.Size == 2)
            {
                tabela[valor & 0xFF] = opcode;
            }
        }

        return tabela;
    }
}
