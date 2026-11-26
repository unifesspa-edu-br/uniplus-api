namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A leitura da identificação do documento: resolve nos cadastros citados o tipo de processo, a unidade administradora, a
/// localidade e a convenção de contagem, e copia a origem dos candidatos e o identificador legível. Código ausente deixa o campo
/// sem valor, e a regra de completude ou a pendência dele o recusa ao publicar; código que o cadastro não tem vira parte recusada,
/// que aponta o campo que o cita sem repetir o texto escrito. A localidade que o Geo não pôde conferir também vira parte recusada,
/// mas com a causa de a conferência não ter sido feita, e não a de município inexistente. Não confere regra.
/// </summary>
internal static class LeituraDaIdentificacao
{
    internal static IdentificacaoLida Ler(IdentificacaoNoDocumento identificacao, CadastrosCitadosNoDocumento cadastros)
    {
        List<ParteRecusada> recusadas = [];
        TipoProcessoSnapshot? tipo = Citado(identificacao.TipoProcessoCodigo, cadastros.TipoDeProcesso, IdentidadeDoProcessoDeclarada.CampoDoTipoDeProcesso, TipoDeProcessoNaoEncontrado, recusadas);
        UnidadeCitada? unidade = Citado(identificacao.UnidadeAdministradoraCodigo, cadastros.Unidade, IdentidadeDoProcessoDeclarada.CampoDaUnidadeAdministradora, UnidadeNaoEncontrada, recusadas);
        LocalidadeRegente? localidade = Citado(
            identificacao.LocalidadeCodigoIbge, cadastros.Localidade, IdentidadeDoProcessoDeclarada.CampoDaLocalidade,
            cadastros.LocalidadeNaoConferida ?? MunicipioNaoEncontrado, recusadas);
        ReferenciaRegra? algoritmo = Algoritmo(identificacao.AlgoritmoContagemPrazo, cadastros, recusadas);
        IdentificadorLegivel? identificador = Identificador(identificacao.IdentificadorLegivel, recusadas);

        return new(
            new IdentidadeDoProcessoDeclarada(identificacao.Nome, identificacao.OrigemCandidatos, identificador, tipo, unidade?.Cadastro),
            localidade,
            algoritmo,
            recusadas);
    }

    private static T? Citado<T>(
        string? codigo, Func<string?, T?> noCadastro, string campo, DomainError naoEncontrado, List<ParteRecusada> recusadas)
        where T : class
    {
        if (codigo is null)
        {
            return null;
        }

        T? achado = noCadastro(codigo);
        if (achado is null)
        {
            recusadas.Add(Recusada(campo, naoEncontrado));
        }

        return achado;
    }

    private static ReferenciaRegra? Algoritmo(ReferenciaDeRegraNoDocumento? citada, CadastrosCitadosNoDocumento cadastros, List<ParteRecusada> recusadas)
    {
        if (citada is null)
        {
            return null;
        }

        RegraDoCatalogoCitada? regra = cadastros.Regra(citada.Codigo, citada.Versao);
        if (regra is { Tipo: TipoRegra.ConvencaoContagemPrazo })
        {
            return regra.Referencia;
        }

        recusadas.Add(new(IdentidadeDoProcessoDeclarada.CampoDaConvencaoDeContagem, null, 0,
            new FieldError(PonteiroNoDocumento.Converter($"{IdentidadeDoProcessoDeclarada.CampoDaConvencaoDeContagem}.codigo", OrigemNoDocumento.Identidade), AlgoritmoNaoEncontrado)));
        return null;
    }

    private static IdentificadorLegivel? Identificador(string? valor, List<ParteRecusada> recusadas)
    {
        if (valor is null)
        {
            return null;
        }

        Result<IdentificadorLegivel> identificador = IdentificadorLegivel.Criar(valor);
        if (identificador.IsFailure)
        {
            recusadas.Add(Recusada(IdentidadeDoProcessoDeclarada.CampoDoIdentificadorLegivel, identificador.Error!));
        }

        return identificador.IsSuccess ? identificador.Value : null;
    }

    private static ParteRecusada Recusada(string campo, DomainError erro) =>
        new(campo, null, 0, new FieldError(PonteiroNoDocumento.Converter(campo, OrigemNoDocumento.Identidade), erro));

    private static DomainError TipoDeProcessoNaoEncontrado { get; } = new(
        ProcessoSeletivoErrorCodes.TipoProcessoNaoEncontradoOuInativo,
        "O tipo de processo citado na identificação não foi encontrado ou não está ativo.");

    private static DomainError UnidadeNaoEncontrada { get; } = new(
        ProcessoSeletivoErrorCodes.UnidadeAdministradoraNaoEncontrada,
        "A unidade administradora citada na identificação não foi encontrada ou não está mais viva.");

    private static DomainError MunicipioNaoEncontrado { get; } = new(
        MunicipioErrorCodes.NaoEncontrado,
        "O município citado como localidade do processo não existe no cadastro de municípios.");

    private static DomainError AlgoritmoNaoEncontrado { get; } = new(
        ProcessoSeletivoErrorCodes.AlgoritmoContagemPrazoNaoEncontrado,
        "A regra citada como convenção de contagem dos prazos não é um algoritmo de contagem do catálogo de regras.");
}
