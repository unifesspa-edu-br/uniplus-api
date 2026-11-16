namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Commands.ObrigatoriedadesLegais;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

internal static class LeitoresDeCadastroParaObrigatoriedadeLegal
{
    internal const string TipoEtapaCodigo = "PROVA_OBJETIVA";

    /// <summary>A conferência de campos e referências montada sobre os leitores dados, como o contêiner a monta.</summary>
    internal static ConferenciaDaObrigatoriedadeLegal Conferencia(
        ITipoProcessoReader tipoProcessoReader,
        ITipoEtapaReader tipoEtapaReader,
        IModalidadeReader modalidadeReader,
        ITipoDocumentoReader tipoDocumentoReader,
        ITipoDeficienciaReader tipoDeficienciaReader,
        IRegraCatalogoReader regraCatalogoReader) =>
        new(
            tipoProcessoReader,
            new ReferenciasDoPredicado(tipoEtapaReader, modalidadeReader, tipoDocumentoReader, tipoDeficienciaReader, regraCatalogoReader));

    internal static ITipoEtapaReader TipoEtapaReaderAtivo() =>
        TipoEtapaReaderComResposta(new TipoEtapaView(Guid.CreateVersion7(), TipoEtapaCodigo, "Prova Objetiva", null, true, true, false, []));

    /// <summary>Leitor que dá por viva qualquer modalidade consultada — o caso comum dos testes de outro assunto.</summary>
    internal static IModalidadeReader ModalidadeReaderViva()
    {
        IModalidadeReader reader = Substitute.For<IModalidadeReader>();
        reader.ObterVivaPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => ModalidadeViva(call.Arg<string>()));
        return reader;
    }

    /// <summary>Leitor que dá por vivo qualquer tipo de documento consultado.</summary>
    internal static ITipoDocumentoReader TipoDocumentoReaderVivo()
    {
        ITipoDocumentoReader reader = Substitute.For<ITipoDocumentoReader>();
        reader.ObterVivoPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => TipoDocumentoVivo(call.Arg<string>()));
        return reader;
    }

    internal static IModalidadeReader ModalidadeReaderSem(string codigoAusente)
    {
        IModalidadeReader reader = Substitute.For<IModalidadeReader>();
        reader.ObterVivaPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<string>() == codigoAusente ? null : ModalidadeViva(call.Arg<string>()));
        return reader;
    }

    internal static ITipoDocumentoReader TipoDocumentoReaderSem(string codigoAusente)
    {
        ITipoDocumentoReader reader = Substitute.For<ITipoDocumentoReader>();
        reader.ObterVivoPorCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<string>() == codigoAusente ? null : TipoDocumentoVivo(call.Arg<string>()));
        return reader;
    }

    private static ModalidadeView ModalidadeViva(string codigo) =>
        new(Guid.CreateVersion7(), codigo, null, "COTA_RESERVADA", "DENTRO_DO_VR", null, null, null, null, null, PredicadoDnf.CriarDeCondicoesAgrupadas([]).Value!, null, null);

    private static TipoDocumentoView TipoDocumentoVivo(string codigo) =>
        new(Guid.CreateVersion7(), codigo, "Documento", "OUTROS", ClassificacaoDeProtecao.Pessoal, "Comprovação documental do candidato.", "CUMPRIMENTO_OBRIGACAO_LEGAL", ProtecaoAprovada: true);

    internal static ITipoEtapaReader TipoEtapaReaderComResposta(TipoEtapaView? resposta)
    {
        ITipoEtapaReader reader = Substitute.For<ITipoEtapaReader>();
        reader.ObterAtivoPorCodigoAsync(TipoEtapaCodigo, Arg.Any<CancellationToken>()).Returns(resposta);
        return reader;
    }

    internal static ITipoProcessoReader TipoProcessoAtivo(string codigo)
    {
        ITipoProcessoReader reader = Substitute.For<ITipoProcessoReader>();
        reader.ObterAtivoPorCodigoAsync(codigo, Arg.Any<CancellationToken>())
            .Returns(new TipoProcessoView(Guid.CreateVersion7(), codigo, "Processo novo", null));
        return reader;
    }
}
