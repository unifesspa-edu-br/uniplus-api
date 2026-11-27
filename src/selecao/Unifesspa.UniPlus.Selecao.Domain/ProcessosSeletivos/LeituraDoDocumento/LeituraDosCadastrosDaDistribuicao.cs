namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Domain.OfertasDeCurso;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A distribuição declarada com o que os cadastros citados deram, os fatos que eles dão sobre a oferta, a oferta de origem e as
/// causas, nos campos da distribuição, que a leitura deles encontrou. O que o cadastro recusou fica fora da declaração e consta em
/// <see cref="DistribuicaoDeVagasDeclarada.Recusados"/>.
/// </summary>
internal sealed record CadastrosDaDistribuicaoLidos(
    DistribuicaoDeVagasDeclarada Declarada,
    FatosDaOfertaNoCadastro Fatos,
    Guid? OfertaOrigemId,
    IReadOnlyList<FieldError> Recusas);

/// <summary>
/// Lê os cadastros que uma distribuição cita, todos de uma vez e sem repetir o texto enviado: a oferta, a regra de distribuição, a
/// regra de ajuste e os parâmetros dele, a referência demográfica e cada modalidade, nesta ordem. A causa de cada um vai no campo do
/// código e, na modalidade, na posição enviada.
/// </summary>
internal static class LeituraDosCadastrosDaDistribuicao
{
    internal static CadastrosDaDistribuicaoLidos Ler(DistribuicaoNoDocumento distribuicao, CadastrosCitadosNoDocumento cadastros)
    {
        ArgumentNullException.ThrowIfNull(distribuicao);
        ArgumentNullException.ThrowIfNull(cadastros);

        List<FieldError> recusas = [];
        HashSet<DadoDaDistribuicaoDeVagas> recusados = [];
        (OfertaDeCursoCitada? oferta, CopiaDaOfertaDeCurso? nome) = Oferta(distribuicao, cadastros, recusas);
        RegraDoCatalogoCitada? regra = RegraDeDistribuicao(distribuicao, cadastros, recusas, recusados);
        DistribuicaoDeVagasDeclarada declarada = Declarada(distribuicao, regra, cadastros, recusas, recusados);

        return new(declarada, Fatos(oferta, nome, regra), oferta?.OrigemId, recusas);
    }

    private static DistribuicaoDeVagasDeclarada Declarada(
        DistribuicaoNoDocumento distribuicao,
        RegraDoCatalogoCitada? regra,
        CadastrosCitadosNoDocumento cadastros,
        List<FieldError> recusas,
        HashSet<DadoDaDistribuicaoDeVagas> recusados) => new()
        {
            RegraDeDistribuicao = regra?.Referencia,
            VagasOfertadasBase = distribuicao.VagasOfertadasBase ?? 0,
            PercentualDaReservaLegal = distribuicao.PercentualDaReservaLegal,
            RegraDeAjuste = RegraDeAjuste(distribuicao, cadastros, recusas, recusados),
            AjusteDoQuadro = AjusteDoQuadro(distribuicao.AjusteDoQuadro, recusas),
            ReferenciaDemografica = ReferenciaDemografica(distribuicao, cadastros, recusas, recusados),
            Modalidades = Modalidades(distribuicao, cadastros, recusas, recusados),
            Recusados = recusados,
        };

    // Ausente é válido; o tipo que não é de nenhuma regra de ajuste conhecida deixa o ajuste fora com a causa no campo dele.
    private static ParametrosRegraAjusteDistribuicao? AjusteDoQuadro(AjusteDoQuadroNoDocumento? ajuste, List<FieldError> recusas)
    {
        ParametrosRegraAjusteDistribuicao? parametros = ajuste?.Tipo switch
        {
            null => null,
            "reduzirDe" => new ParametrosReduzirDe(ajuste.ModalidadeCodigo ?? string.Empty),
            "reduzirProporcionalEm" => new ParametrosReduzirProporcionalEm(ajuste.ModalidadeCodigos),
            _ => null,
        };
        if (ajuste is not null && parametros is null)
        {
            recusas.Add(new("ajusteDoQuadro", RecusasDeCadastroDasVagas.AjusteDoQuadroDesconhecido()));
        }

        return parametros;
    }

    private static FatosDaOfertaNoCadastro Fatos(OfertaDeCursoCitada? oferta, CopiaDaOfertaDeCurso? nome, RegraDoCatalogoCitada? regra) => new()
    {
        VagasAnuaisAutorizadas = oferta?.VagasAnuaisAutorizadas,
        ModalidadesAdmitidas = regra is null ? null : ModalidadesAdmitidasDaDeclaracaoDaRegra.Extrair(regra.DeclaracaoDaRegra),
        GrupoAreaEnem = oferta?.GrupoAreaEnemCodigo is null ? null : (oferta.GrupoAreaEnemCodigo, oferta.GrupoAreaEnemRotulo),
        OfertaCurso = nome,
    };

    private static (OfertaDeCursoCitada? Oferta, CopiaDaOfertaDeCurso? Nome) Oferta(
        DistribuicaoNoDocumento distribuicao, CadastrosCitadosNoDocumento cadastros, List<FieldError> recusas)
    {
        OfertaDeCursoCitada? oferta = cadastros.OfertaDeCurso(distribuicao.OfertaCursoCodigo);
        if (oferta is null)
        {
            recusas.Add(new("ofertaCursoId", RecusasDeCadastroDasVagas.OfertaNaoEncontrada()));
            return (null, null);
        }

        Result<CopiaDaOfertaDeCurso> nome = CopiaDaOfertaDeCurso.Criar(
            oferta.Codigo, oferta.CursoNome, oferta.CursoGrau, oferta.UnidadeOfertanteNome, oferta.Turnos);
        recusas.AddRange(nome.Errors.Select(static causa => new FieldError("ofertaCursoId", causa.Error)));

        return (oferta, nome.Value);
    }

    private static RegraDoCatalogoCitada? RegraDeDistribuicao(
        DistribuicaoNoDocumento distribuicao, CadastrosCitadosNoDocumento cadastros, List<FieldError> recusas, HashSet<DadoDaDistribuicaoDeVagas> recusados)
    {
        RegraDoCatalogoCitada? regra = cadastros.Regra(distribuicao.RegraDeDistribuicao?.Codigo, distribuicao.RegraDeDistribuicao?.Versao);
        DomainError? recusa = regra switch
        {
            null => RecusasDeCadastroDasVagas.RegraDeDistribuicaoNaoEncontrada(),
            { Tipo: not TipoRegra.RegraDistribuicaoVagas } => RecusasDeCadastroDasVagas.RegraDeDistribuicaoDeTipoInvalido(),
            _ => null,
        };

        return Recusar(recusa, "regraDistribuicaoCodigo", DadoDaDistribuicaoDeVagas.RegraDeDistribuicao, recusas, recusados) ? null : regra;
    }

    // Ausência é válida: a conferência da distribuição decide se a regra de ajuste é obrigatória pelo ramo da regra de distribuição.
    private static ReferenciaRegra? RegraDeAjuste(
        DistribuicaoNoDocumento distribuicao, CadastrosCitadosNoDocumento cadastros, List<FieldError> recusas, HashSet<DadoDaDistribuicaoDeVagas> recusados)
    {
        if (distribuicao.RegraDeAjuste is not { } citada)
        {
            return null;
        }

        RegraDoCatalogoCitada? regra = cadastros.Regra(citada.Codigo, citada.Versao);
        DomainError? recusa = regra switch
        {
            null => RecusasDeCadastroDasVagas.RegraDeAjusteNaoEncontrada(),
            { Tipo: not TipoRegra.RegraAjusteDistribuicaoVagas } => RecusasDeCadastroDasVagas.RegraDeAjusteDeTipoInvalido(),
            _ => null,
        };

        return Recusar(recusa, "regraAjusteCodigo", DadoDaDistribuicaoDeVagas.RegraDeAjuste, recusas, recusados) ? null : regra!.Referencia;
    }

    private static ReferenciaReservaDemograficaSnapshot? ReferenciaDemografica(
        DistribuicaoNoDocumento distribuicao, CadastrosCitadosNoDocumento cadastros, List<FieldError> recusas, HashSet<DadoDaDistribuicaoDeVagas> recusados)
    {
        if (distribuicao.ReferenciaDemograficaCodigo is null)
        {
            return null;
        }

        ReferenciaReservaDemograficaSnapshot? referencia = cadastros.ReferenciaDemografica(distribuicao.ReferenciaDemograficaCodigo);
        DomainError? recusa = referencia is null ? RecusasDeCadastroDasVagas.ReferenciaDemograficaNaoEncontrada() : null;

        return Recusar(recusa, "referenciaReservaDemograficaId", DadoDaDistribuicaoDeVagas.ReferenciaDemografica, recusas, recusados) ? null : referencia;
    }

    private static List<ModalidadeSelecionada> Modalidades(
        DistribuicaoNoDocumento distribuicao, CadastrosCitadosNoDocumento cadastros, List<FieldError> recusas, HashSet<DadoDaDistribuicaoDeVagas> recusados)
    {
        (ModalidadeNoDocumento Documento, Result<ModalidadeSelecionada> Criada)[] lidas =
            [.. distribuicao.Modalidades.Select(modalidade => (modalidade, Modalidade(modalidade, cadastros)))];
        recusas.AddRange(lidas.SelectMany(lida => lida.Criada.Errors.Select(causa => causa.NoCaminhoDoItem($"modalidadeIds[{lida.Documento.PosicaoNoDocumento}]"))));
        if (lidas.Any(static lida => lida.Criada.IsFailure))
        {
            recusados.Add(DadoDaDistribuicaoDeVagas.Modalidades);
        }

        return [.. lidas.Where(static lida => lida.Criada.IsSuccess).Select(static lida => lida.Criada.Value!)];
    }

    private static Result<ModalidadeSelecionada> Modalidade(ModalidadeNoDocumento modalidade, CadastrosCitadosNoDocumento cadastros) =>
        cadastros.Modalidade(modalidade.Codigo) is { } cadastro
            ? ModalidadeSelecionada.Criar(cadastro, modalidade.QuantidadeDeclarada)
            : Result<ModalidadeSelecionada>.Failure(RecusasDeCadastroDasVagas.ModalidadeNaoEncontrada());

    private static bool Recusar(
        DomainError? recusa, string campo, DadoDaDistribuicaoDeVagas dado, List<FieldError> recusas, HashSet<DadoDaDistribuicaoDeVagas> recusados)
    {
        if (recusa is null)
        {
            return false;
        }

        recusas.Add(new(campo, recusa));
        recusados.Add(dado);

        return true;
    }
}
