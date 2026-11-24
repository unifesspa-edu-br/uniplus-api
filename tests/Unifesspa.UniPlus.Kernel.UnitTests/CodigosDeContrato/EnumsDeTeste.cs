namespace Unifesspa.UniPlus.Kernel.UnitTests.CodigosDeContrato;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

[DescricaoDoContrato("As cores do teste.")]
public enum Cor
{
    [CodigoDeContrato("AZUL", "Azul", Descricao = "A cor do céu.")]
    Azul = 1,

    [CodigoDeContrato("VERDE_CLARO", "Verde claro")]
    VerdeClaro = 2,
}

public enum SemAtributoEmUmMembro
{
    [CodigoDeContrato("A", "A")]
    A = 1,

    B = 2,
}

public enum ComCodigoApenasEspaco
{
    [CodigoDeContrato(" ", "Vazio")]
    Vazio = 1,
}

public enum ComCodigoVazio
{
    [CodigoDeContrato("", "Vazio")]
    Vazio = 1,
}

public enum ComCodigoRepetido
{
    [CodigoDeContrato("IGUAL", "Primeiro")]
    Primeiro = 1,

    [CodigoDeContrato("IGUAL", "Segundo")]
    Segundo = 2,
}

public enum ComEspacoNoCodigo
{
    [CodigoDeContrato(" APARADO", "Com espaço")]
    ComEspaco = 1,
}

public enum ComEspacoNoRotulo
{
    [CodigoDeContrato("COM_ESPACO", " Com espaço ")]
    ComEspaco = 1,
}

public enum ComRotuloVazio
{
    [CodigoDeContrato("SEM_ROTULO", "")]
    SemRotulo = 1,
}

public enum ComRotuloRepetido
{
    [CodigoDeContrato("A", "Mesmo rótulo")]
    A = 1,

    [CodigoDeContrato("B", "MESMO RÓTULO")]
    B = 2,
}

public enum ComDescricaoVazia
{
    [CodigoDeContrato("COM_DESCRICAO_VAZIA", "Descrição vazia", Descricao = " ")]
    DescricaoVazia = 1,
}

public enum ComVariosDefeitos
{
    [CodigoDeContrato("REPETIDO", "Rótulo")]
    A = 1,

    [CodigoDeContrato("REPETIDO", "rótulo")]
    B = 2,

    C = 3,
}

public enum ComEspacoERepetido
{
    [CodigoDeContrato(" IGUAL", "Primeiro")]
    Primeiro = 1,

    [CodigoDeContrato(" IGUAL", "Segundo")]
    Segundo = 2,
}

public enum ComBaseUlong : ulong
{
    [CodigoDeContrato("GRANDE", "Grande")]
    Grande = ulong.MaxValue,
}

[DescricaoDoContrato(" ")]
public enum ComDescricaoDoEnumVazia
{
    [CodigoDeContrato("UNICO", "Único")]
    Unico = 1,
}

public enum SemAtributoDeDescricao
{
    [CodigoDeContrato("UNICO", "Único")]
    Unico = 1,
}

public enum Prioridade
{
    [CodigoDeContrato("BAIXA", "Baixa")]
    Baixa = 30,

    [CodigoDeContrato("ALTA", "Alta")]
    Alta = 10,

    [CodigoDeContrato("MEDIA", "Média")]
    Media = 20,
}

public enum SemCodigoDeContrato
{
    Primeiro = 1,
    Segundo = 2,
}
