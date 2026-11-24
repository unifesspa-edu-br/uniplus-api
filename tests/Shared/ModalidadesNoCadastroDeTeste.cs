namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

internal static class ModalidadesNoCadastroDeTeste
{
    internal static ModalidadeNoCadastro Da(string codigo, NaturezaLegalModalidade natureza, ComposicaoVagasModalidade composicao) => new()
    {
        Id = Guid.CreateVersion7(),
        Codigo = codigo,
        NaturezaLegal = natureza,
        ComposicaoVagas = composicao,
        RegraRemanejamento = natureza == NaturezaLegalModalidade.CotaReservada ? RegraRemanejamentoModalidade.SegueOrdemDeRemanejamento : null,
        RegraDeAplicabilidade = RegrasDeAplicabilidadeDeTeste.DaNatureza(natureza),
        BaseLegal = "Lei 12.711/2012",
    };
}
