namespace Unifesspa.UniPlus.Autorizacao.Apoio;

using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Catálogo cuja sensibilidade e alcance os testes mudam entre a criação e a decisão da versão: <c>dados</c> é pessoal
/// e as permissões <c>unidade:</c> são de unidade; o resto é interno.
/// </summary>
public sealed class CatalogoMutavelDeTeste : ICatalogoDePermissoesConsultavel
{
    public const string Dados = "selecao:inscricoes:ler-dados-pessoais";

    public ClassificacaoDeProtecao SensibilidadeDosDados { get; set; } = ClassificacaoDeProtecao.Pessoal;

    public HashSet<string> Inexistentes { get; } = [];

    public HashSet<string> Reservadas { get; } = [];

    public HashSet<string> DeUnidadeExtras { get; } = [];

    public bool Existe(string codigo) => !Inexistentes.Contains(codigo);

    public bool ReservadaAGovernanca(string codigo) => Reservadas.Contains(codigo);

    public bool DeUnidade(string codigo)
    {
        ArgumentNullException.ThrowIfNull(codigo);

        return codigo.StartsWith("unidade:", StringComparison.Ordinal) || DeUnidadeExtras.Contains(codigo);
    }

    public ClassificacaoDeProtecao Sensibilidade(string codigo) =>
        codigo == Dados ? SensibilidadeDosDados : ClassificacaoDeProtecao.Interno;
}
