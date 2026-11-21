namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Bancas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.OrdemCanonica;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// As bancas saem pelo código do tipo e pela posição dentro dele. O id da banca e a origem do tipo não decidem a ordem, nem como
/// desempate: o par código e posição é único no lugar.
/// </summary>
public sealed class OrdemCanonicaDasBancasTests
{
    private static readonly Guid IdMenor = new("00000000-0000-4000-8000-000000000001");
    private static readonly Guid IdMaior = new("00000000-0000-4000-8000-000000000002");

    private static BancaDaEtapa NovaBancaDaEtapa(TipoDeBanca tipo) =>
        BancaDaEtapa.Criar(TiposDeBancaCopiadosDeTeste.De(tipo));

    private static BancaRequerida NovaBancaDaFase(TipoDeBanca tipo, Guid origem) =>
        BancaRequerida.Criar(TiposDeBancaCopiadosDeTeste.De(tipo, origem), []);

    [Fact(DisplayName = "As bancas da etapa saem pelo código do tipo e, dentro dele, pela posição")]
    public void BancasDaEtapa_CodigoEPosicao()
    {
        List<BancaDaEtapa> bancas = ReconciliacaoDasBancas.Reconciliar<BancaDaEtapa>(
            [], [NovaBancaDaEtapa(TipoDeBanca.Entrevista), NovaBancaDaEtapa(TipoDeBanca.AnaliseDocumental), NovaBancaDaEtapa(TipoDeBanca.Entrevista), NovaBancaDaEtapa(TipoDeBanca.AnaliseDocumental)]);

        OrdemCanonicaDaConfiguracao.BancasDaEtapa(bancas.AsEnumerable().Reverse())
            .Select(static b => (b.Tipo.Codigo, b.PosicaoNoTipo))
            .Should().Equal((TipoDeBanca.AnaliseDocumental, 1), (TipoDeBanca.AnaliseDocumental, 2), (TipoDeBanca.Entrevista, 1), (TipoDeBanca.Entrevista, 2));
    }

    [Fact(DisplayName = "O id da banca da etapa não decide a ordem: com o id em ordem inversa à da posição, a ordem segue a posição")]
    public void BancasDaEtapa_IdEmOrdemInversaAPosicao_SeguePosicao()
    {
        BancaDaEtapa idMenor = BancaDaEtapa.ReconstruirDosDadosSalvos(IdMenor, TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.AnaliseDocumental, Guid.CreateVersion7()));
        BancaDaEtapa idMaior = BancaDaEtapa.ReconstruirDosDadosSalvos(IdMaior, TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.AnaliseDocumental, Guid.CreateVersion7()));

        ReconciliacaoDasBancas.Reconciliar<BancaDaEtapa>([], [idMaior, idMenor]);

        OrdemCanonicaDaConfiguracao.BancasDaEtapa([idMenor, idMaior]).Should().Equal(idMaior, idMenor);
    }

    [Fact(DisplayName = "As bancas da fase saem pelo código do tipo, e não pela origem do tipo no cadastro")]
    public void BancasDaFase_OrigensEmOrdemInversaADosCodigos_SegueOCodigo()
    {
        Guid origemMenor = Guid.CreateVersion7();
        Guid origemMaior = Guid.CreateVersion7();
        BancaRequerida entrevista = NovaBancaDaFase(TipoDeBanca.Entrevista, origemMenor);
        BancaRequerida analise = NovaBancaDaFase(TipoDeBanca.AnaliseDocumental, origemMaior);
        ReconciliacaoDasBancas.Reconciliar<BancaRequerida>([], [entrevista, analise]);

        OrdemCanonicaDaConfiguracao.BancasDaFase([entrevista, analise]).Should().Equal(analise, entrevista);
    }

    [Fact(DisplayName = "O id da banca da fase não decide a ordem: com o id em ordem inversa à da posição, a ordem segue a posição")]
    public void BancasDaFase_IdEmOrdemInversaAPosicao_SeguePosicao()
    {
        BancaRequerida idMenor = BancaRequerida.ReconstruirDosDadosSalvos(IdMenor, TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.AnaliseDocumental, Guid.CreateVersion7()), []);
        BancaRequerida idMaior = BancaRequerida.ReconstruirDosDadosSalvos(IdMaior, TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.AnaliseDocumental, Guid.CreateVersion7()), []);

        ReconciliacaoDasBancas.Reconciliar<BancaRequerida>([], [idMaior, idMenor]);

        OrdemCanonicaDaConfiguracao.BancasDaFase([idMenor, idMaior]).Should().Equal(idMaior, idMenor);
    }

    [Fact(DisplayName = "Trocar as posições de duas bancas inverte a ordem delas sem trocar o id de nenhuma")]
    public void BancasDaEtapa_TrocaDePosicoes_InverteAOrdem()
    {
        BancaDaEtapa primeira = NovaBancaDaEtapa(TipoDeBanca.AnaliseDocumental);
        BancaDaEtapa segunda = NovaBancaDaEtapa(TipoDeBanca.AnaliseDocumental);
        List<BancaDaEtapa> gravadas = ReconciliacaoDasBancas.Reconciliar<BancaDaEtapa>([], [primeira, segunda]);
        Guid[] ids = [primeira.Id, segunda.Id];

        ReconciliacaoDasBancas.Reconciliar(
            gravadas,
            [BancaDaEtapa.Criar(TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.AnaliseDocumental, Guid.CreateVersion7()), idDaBancaQueSubstitui: segunda.Id),
             BancaDaEtapa.Criar(TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.AnaliseDocumental, Guid.CreateVersion7()), idDaBancaQueSubstitui: primeira.Id)]);

        OrdemCanonicaDaConfiguracao.BancasDaEtapa(gravadas).Select(static b => b.Id).Should().Equal(ids[1], ids[0]);
    }
}
