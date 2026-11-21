namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

/// <summary>
/// O que a reconciliação precisa saber de uma banca da fase ou da etapa: quem ela é, de que tipo é, qual banca já gravada a
/// declaração substitui e como assume a declaração.
/// </summary>
internal interface IBancaDoLugar<in TBanca>
    where TBanca : class
{
    Guid Id { get; }

    string Codigo { get; }

    Guid? IdDaBancaQueSubstitui { get; }

    void PosicionarNoTipo(int posicaoNoTipo);

    void AssumirDeclaracao(TBanca declarada);
}
