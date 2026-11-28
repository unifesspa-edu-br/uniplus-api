namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

internal static class ModalidadesNoCadastroDeTeste
{
    internal static ModalidadeNoCadastro Da(string codigo, NaturezaLegalModalidade natureza, ComposicaoVagasModalidade composicao) => new()
    {
        Id = Guid.CreateVersion7(),
        Codigo = codigo,
        Nome = NomeDe(codigo),
        NaturezaLegal = natureza,
        ComposicaoVagas = composicao,
        RegraRemanejamento = natureza == NaturezaLegalModalidade.CotaReservada ? RegraRemanejamentoModalidade.SegueOrdemDeRemanejamento : null,
        RegraDeAplicabilidade = RegrasDeAplicabilidadeDeTeste.DaNatureza(natureza),
        BaseLegal = "Lei 12.711/2012",
    };

    internal static string NomeDe(string codigo) => codigo switch
    {
        "AC" => "Ampla concorrência",
        "LB_PPI" => "Escola pública, renda de até 1 salário mínimo por pessoa, pretos, pardos e indígenas",
        "LB_Q" => "Escola pública, renda de até 1 salário mínimo por pessoa, quilombolas",
        "LB_PCD" => "Escola pública, renda de até 1 salário mínimo por pessoa, pessoas com deficiência",
        "LB_EP" => "Escola pública, renda de até 1 salário mínimo por pessoa",
        "LI_PPI" => "Escola pública, qualquer renda, pretos, pardos e indígenas",
        "LI_Q" => "Escola pública, qualquer renda, quilombolas",
        "LI_PCD" => "Escola pública, qualquer renda, pessoas com deficiência",
        "LI_EP" => "Escola pública, qualquer renda",
        "AC_PCD" => "Ampla concorrência, ação afirmativa da Unifesspa para pessoas com deficiência",
        "AC_I" => "Ampla concorrência, ação afirmativa da Unifesspa para indígenas",
        "AC_Q" => "Ampla concorrência, ação afirmativa da Unifesspa para quilombolas",
        _ => $"Modalidade {codigo}",
    };
}
