namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Monta o laudo médico complementar, um tipo de documento pessoal, na situação da aprovação que o cenário pede. Quem
/// envia é Isabella e quem decide é Helena, Representante do controlador.
/// </summary>
internal sealed class TipoDocumentoNaAprovacaoBuilder
{
    public const string Finalidade = "Comprovação documental do candidato.";
    public const string Isabella = "isabella.figueiredo";
    public const string Helena = "helena.rezende";

    public static readonly DateTimeOffset Segunda = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    private SituacaoDaAprovacao _situacao = SituacaoDaAprovacao.Pendente;

    public TipoDocumentoNaAprovacaoBuilder Solicitado() => Em(SituacaoDaAprovacao.Solicitada);

    public TipoDocumentoNaAprovacaoBuilder Aprovado() => Em(SituacaoDaAprovacao.Aprovada);

    public TipoDocumentoNaAprovacaoBuilder ComAjusteRecomendado() => Em(SituacaoDaAprovacao.AjusteRecomendado);

    public TipoDocumentoNaAprovacaoBuilder Reprovado() => Em(SituacaoDaAprovacao.Reprovada);

    public TipoDocumento Build()
    {
        TipoDocumento tipo = TipoDocumento.Criar(
            "LAUDO_MEDICO_COMPLEMENTAR", "Laudo médico complementar", null, "SAUDE", null, null, null,
            ClassificacaoDeProtecao.Pessoal, Finalidade, HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;
        if (_situacao != SituacaoDaAprovacao.Pendente)
        {
            tipo.SolicitarAprovacaoDaProtecao(Isabella, Segunda);
        }

        Decidir(tipo);
        return tipo;
    }

    /// <summary>Edita o tipo mudando só o nome: não mexe na proteção.</summary>
    public static void EditarONome(TipoDocumento tipo) =>
        tipo.Atualizar(
            "LAUDO_MEDICO_COMPLEMENTAR", "Laudo médico", null, "SAUDE", null, null, null,
            tipo.ClassificacaoProtecao, Finalidade, tipo.HipoteseLegal);

    /// <summary>Edita o tipo mudando a classificação para sensível, com a hipótese do art. 11.</summary>
    public static void EditarAProtecao(TipoDocumento tipo) =>
        tipo.Atualizar(
            "LAUDO_MEDICO_COMPLEMENTAR", "Laudo médico complementar", null, "SAUDE", null, null, null,
            ClassificacaoDeProtecao.Sensivel, Finalidade, HipoteseLegalTratamento.TutelaDaSaude);

    private TipoDocumentoNaAprovacaoBuilder Em(SituacaoDaAprovacao situacao)
    {
        _situacao = situacao;
        return this;
    }

    private void Decidir(TipoDocumento tipo)
    {
        DateTimeOffset terca = Segunda.AddDays(1);
        _ = _situacao switch
        {
            SituacaoDaAprovacao.Aprovada => tipo.AprovarProtecao(Helena, terca),
            SituacaoDaAprovacao.AjusteRecomendado => tipo.RecomendarAjusteDaProtecao(ClassificacaoDeProtecao.Sensivel, null, Helena, terca),
            SituacaoDaAprovacao.Reprovada => tipo.ReprovarProtecao("O laudo não é necessário para a finalidade.", Helena, terca),
            _ => null,
        };
    }
}
