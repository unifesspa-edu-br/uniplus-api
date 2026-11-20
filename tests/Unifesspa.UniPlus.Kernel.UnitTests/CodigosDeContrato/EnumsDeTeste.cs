namespace Unifesspa.UniPlus.Kernel.UnitTests.CodigosDeContrato;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

[DescricaoDoContrato("As cores do teste.")]
public enum Cor
{
    [CodigoDeContrato("azul", "Azul", Descricao = "A cor do céu.")]
    Azul = 1,

    [CodigoDeContrato("verde_claro", "Verde claro")]
    VerdeClaro = 2,
}

public enum SemAtributoEmUmMembro
{
    [CodigoDeContrato("a", "A")]
    A = 1,

    B = 2,
}

public enum ComCodigoVazio
{
    [CodigoDeContrato(" ", "Vazio")]
    Vazio = 1,
}

public enum ComCodigoRepetido
{
    [CodigoDeContrato("igual", "Primeiro")]
    Primeiro = 1,

    [CodigoDeContrato("igual", "Segundo")]
    Segundo = 2,
}

public enum ComEspacoNoCodigo
{
    [CodigoDeContrato(" aparado", "Com espaço")]
    ComEspaco = 1,
}

public enum ComEspacoNoRotulo
{
    [CodigoDeContrato("com_espaco", " Com espaço ")]
    ComEspaco = 1,
}

public enum ComRotuloVazio
{
    [CodigoDeContrato("sem_rotulo", "")]
    SemRotulo = 1,
}

public enum ComRotuloRepetido
{
    [CodigoDeContrato("a", "Mesmo rótulo")]
    A = 1,

    [CodigoDeContrato("b", "MESMO RÓTULO")]
    B = 2,
}

public enum ComDescricaoVazia
{
    [CodigoDeContrato("com_descricao_vazia", "Descrição vazia", Descricao = " ")]
    DescricaoVazia = 1,
}

public enum ComVariosDefeitos
{
    [CodigoDeContrato("repetido", "Rótulo")]
    A = 1,

    [CodigoDeContrato("repetido", "rótulo")]
    B = 2,

    C = 3,
}

public enum ComEspacoERepetido
{
    [CodigoDeContrato(" igual", "Primeiro")]
    Primeiro = 1,

    [CodigoDeContrato(" igual", "Segundo")]
    Segundo = 2,
}

public enum ComBaseUlong : ulong
{
    [CodigoDeContrato("grande", "Grande")]
    Grande = ulong.MaxValue,
}

[DescricaoDoContrato(" ")]
public enum ComDescricaoDoEnumVazia
{
    [CodigoDeContrato("unico", "Único")]
    Unico = 1,
}

public enum SemAtributoDeDescricao
{
    [CodigoDeContrato("unico", "Único")]
    Unico = 1,
}
