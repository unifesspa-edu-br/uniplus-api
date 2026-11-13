namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;

/// <summary>
/// A unidade da Unifesspa que responde por cada fase: o <see cref="DefinirCronogramaFasesCommandHandler"/> a
/// resolve no cadastro, grava o snapshot na fase e recusa a que o cadastro não tem mais.
/// </summary>
public sealed class DefinirCronogramaFasesUnidadeResponsavelTests
{
    [Fact(DisplayName = "A unidade responsável informada é resolvida no cadastro e gravada na fase, com id, sigla e nome")]
    public async Task Handle_UnidadeResponsavelInformada_GravaOSnapshotNaFase()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho()
            .ComTipoDeAto(TipoAtoDaClassificacao())
            .ComUnidadeNoCadastro(UnidadesDeTeste.Cga);
        FaseCronogramaInput fase = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal());

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(
            fase with { UnidadeResponsavelId = UnidadesDeTeste.Cga.OrigemId });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        cenario.Processo.CronogramaFases.Single().UnidadeResponsavel.Should().Be(UnidadesDeTeste.Cga);
    }

    [Fact(DisplayName = "O rascunho sem unidade responsável não consulta o cadastro e grava a fase sem ela")]
    public async Task Handle_SemUnidadeResponsavel_NaoConsultaOCadastroEAceita()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().ComTipoDeAto(TipoAtoDaClassificacao());
        FaseCronogramaInput fase = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal());

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(fase);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        cenario.Processo.CronogramaFases.Single().UnidadeResponsavel.Should().BeNull();
        await cenario.UnidadeResponsavel.DidNotReceiveWithAnyArgs().ResolverAsync(default, default);
    }

    [Fact(DisplayName = "No rascunho, a unidade gravada é resolvida de novo no cadastro, e a que saiu dele é recusada")]
    public async Task Handle_RascunhoComUnidadeGravadaQueSaiuDoCadastro_Recusa()
    {
        (CenarioDoCronogramaDeFases cenario, DefinirCronogramaFasesCommand comando) =
            CadastroSemAUnidadeGravada(ProcessoComUnidadeGravada());

        Result<MutacaoAceita> resultado = await cenario.DefinirAsync(comando);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("FaseCronograma.UnidadeResponsavelNaoEncontrada");
    }

    [Fact(DisplayName = "Na retificação, a unidade que saiu do cadastro é recusada citando a fase, como no rascunho")]
    public async Task Handle_RetificacaoComUnidadeQueSaiuDoCadastro_Recusa()
    {
        ProcessoSeletivo processo = ProcessoComUnidadeGravada();
        PublicacaoDeTeste.AbrirRetificacao(processo, PublicacaoDeTeste.Publicar(processo));
        (CenarioDoCronogramaDeFases cenario, DefinirCronogramaFasesCommand comando) = CadastroSemAUnidadeGravada(processo);

        Result<MutacaoAceita> resultado = await cenario.DefinirAsync(comando);

        resultado.IsFailure.Should().BeTrue();
        FieldError recusa = resultado.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("fases[0].unidadeResponsavelId");
        recusa.Error.Code.Should().Be("FaseCronograma.UnidadeResponsavelNaoEncontrada");
    }

    [Fact(DisplayName = "Duas fases sob a mesma unidade nova resolvem o cadastro uma vez só")]
    public async Task Handle_MesmaUnidadeEmDuasFases_ResolveUmaVez()
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().ComUnidadeNoCadastro(UnidadesDeTeste.Cga);
        FaseCronogramaInput primeira = FaseSemProdutosSobACga(cenario, FaseCanonicaDoResultadoFinal(), ordem: 1);
        FaseCronogramaInput segunda = FaseSemProdutosSobACga(cenario, FaseCanonicaRecorrivel(), ordem: 2);

        await cenario.DefinirFasesAsync(primeira, segunda);

        await cenario.UnidadeResponsavel.Received(1).ResolverAsync(UnidadesDeTeste.Cga.OrigemId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Unidade responsável que não existe no cadastro é recusada no campo da fase, citando a fase")]
    public async Task Handle_UnidadeResponsavelNaoEncontrada_RecusaNoCampoDaFase()
    {
        Guid unidadeExcluida = Guid.CreateVersion7();
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho().SemAUnidadeNoCadastro(unidadeExcluida);
        FaseCronogramaInput fase = cenario.CadastrarFase(FaseCanonicaDoResultadoFinal());

        Result<MutacaoAceita> resultado = await cenario.DefinirFasesAsync(fase with { UnidadeResponsavelId = unidadeExcluida });

        resultado.IsFailure.Should().BeTrue();
        FieldError recusa = resultado.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("fases[0].unidadeResponsavelId");
        recusa.Error.Code.Should().Be("FaseCronograma.UnidadeResponsavelNaoEncontrada");
        recusa.Error.Message.Should().Contain("RESULTADO_FINAL");
        cenario.Processo.CronogramaFases.Should().BeEmpty("a recusa não grava o cronograma");
    }

    private static FaseCronogramaInput FaseSemProdutosSobACga(CenarioDoCronogramaDeFases cenario, FaseCanonicaView faseCanonica, int ordem) =>
        cenario.CadastrarFase(faseCanonica) with
        {
            Ordem = ordem,
            Produtos = [],
            UnidadeResponsavelId = UnidadesDeTeste.Cga.OrigemId,
        };

    private static ProcessoSeletivo ProcessoComUnidadeGravada()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS unidade gravada");
        FaseCronograma conforme = processo.CronogramaFases.Single();
        FaseCronograma comCga = FaseCronograma.Criar(
            conforme.Ordem, conforme.FaseCanonicaOrigemId, conforme.Codigo, conforme.OrigemData,
            conforme.AgrupaEtapas, conforme.PermiteComplementacao, conforme.ColetaInscricao, conforme.ColetaSolicitacaoIsencao,
            conforme.Inicio, conforme.Fim, [.. conforme.Produtos], conforme.FaseConcluinteCodigo, conforme.EmiteParecerIndividual,
            [.. conforme.BancasRequeridas], conforme.RegraRecurso, UnidadesDeTeste.Cga).Value!;
        processo.DefinirCronogramaFases([comCga], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static (CenarioDoCronogramaDeFases Cenario, DefinirCronogramaFasesCommand Comando) CadastroSemAUnidadeGravada(ProcessoSeletivo processo)
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.Para(processo)
            .ComTipoDeAto(TipoAtoDaClassificacao())
            .SemAUnidadeNoCadastro(UnidadesDeTeste.Cga.OrigemId);
        FaseCronogramaInput cadastrada = cenario.CadastrarFase(
            FaseCanonicaDoResultadoFinal() with { Id = processo.CronogramaFases.Single().FaseCanonicaOrigemId });
        FaseCronogramaInput gravada = cadastrada with { UnidadeResponsavelId = UnidadesDeTeste.Cga.OrigemId };

        return (cenario, new DefinirCronogramaFasesCommand(processo.Id, [gravada], PrecondicaoIfMatch.Curinga));
    }
}
