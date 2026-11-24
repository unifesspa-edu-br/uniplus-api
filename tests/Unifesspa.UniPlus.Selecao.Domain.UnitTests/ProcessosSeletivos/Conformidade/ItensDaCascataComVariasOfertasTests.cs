namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioConformidadePublicabilidadeEstrutural;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDasCotasFederaisNaConformidade;

public sealed class ItensDaCascataComVariasOfertasTests
{
    [Fact(DisplayName = "Cascata apresenta as recusas das duas ofertas")]
    public void Avaliar_OfertasComRecusasDiferentes_ApresentaAmbas()
    {
        ProcessoSeletivo processo = ProcessoConforme([OfertaForaDoRegimeFederal(), OfertaFederalCompleta()]);
        SoEstesItensVermelhos(processo, "cascata_pendente", "cascata_modalidade_fora_do_regime_federal", "cascata_origem_ausente");
    }

    private static ConfiguracaoDistribuicaoVagas OfertaForaDoRegimeFederal() => ConfiguracaoDistribuicaoVagas.Criar(
        Guid.CreateVersion7(), new DistribuicaoDeVagasDeclarada
        {
            VagasOfertadasBase = 10,
            PercentualDaReservaLegal = 1m,
            RegraDeDistribuicao = Regra(RegraDeDistribuicaoDeVagas.Institucional.Codigo(), 'a'),
            Modalidades = [Modalidade("AC", NaturezaLegalModalidade.Ampla, null, 2),
                .. OrigensFederais.Select(codigo => Modalidade(codigo, NaturezaLegalModalidade.CotaReservada, RegraRemanejamentoModalidade.SegueOrdemDeRemanejamento, 1))],
        }, new FatosDaOfertaNoCadastro { OfertaCurso = OfertaCursoDeTeste.Medicina }).Value!;
}
