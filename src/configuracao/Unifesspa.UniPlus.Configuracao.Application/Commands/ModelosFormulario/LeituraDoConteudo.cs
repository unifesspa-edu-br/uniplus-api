namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Uma leitura da entrada do conteúdo do modelo: acumula as recusas, na ordem em que as partes são
/// lidas, e guarda a posição de cada parte lida na lista da entrada. A parte nula ou com recusa não
/// é lida; a recusa dela fica nas recusas da leitura.
/// </summary>
/// <param name="formatos">O formato do campo de texto de cada fato, pelo código na forma gravada.</param>
internal sealed class LeituraDoConteudo(IReadOnlyDictionary<string, string> formatos)
{
    private const string MensagemDaEntradaNula = "A etapa, o item, o grupo ou o termo veio nulo.";

    private readonly List<FieldError> _recusas = [];

    /// <summary>
    /// Lê as partes na ordem dos argumentos, que é a ordem das recusas: etapas, itens, termos, grupos,
    /// documentos.
    /// </summary>
    public ConteudoLido Ler(ConteudoDoModeloInput? entrada) => new(
        entrada?.Titulo,
        Lista(entrada?.Etapas, "etapas", LerEtapa),
        Lista(entrada?.Itens, "itens", LerItem),
        Lista(entrada?.Termos, "termos", LerTermo),
        [.. (entrada?.Pressupostos ?? []).Select(static p => p ?? string.Empty)],
        Lista(entrada?.Grupos, "grupos", LerGrupo),
        Lista(entrada?.DocumentosExigidos, "documentosExigidos", LerDocumento),
        _recusas);

    private List<(int Indice, TParte Parte)> Lista<TEntrada, TParte>(
        IReadOnlyList<TEntrada?>? entradas, string nomeDaLista, Func<TEntrada, string, TParte?> ler)
        where TEntrada : class
        where TParte : class
    {
        List<(int, TParte)> lidas = [];
        foreach ((int indice, TEntrada? entrada) in (entradas ?? []).Index())
        {
            if (LerPresente(entrada, $"{nomeDaLista}[{indice}]", ler) is { } parte)
            {
                lidas.Add((indice, parte));
            }
        }

        return lidas;
    }

    private TParte? LerPresente<TEntrada, TParte>(TEntrada? entrada, string campo, Func<TEntrada, string, TParte?> ler)
        where TEntrada : class
        where TParte : class
    {
        if (entrada is null)
        {
            RecusarEntradaMalformada(campo, MensagemDaEntradaNula);
            return null;
        }

        return ler(entrada, campo);
    }

    private EtapaDoModelo? LerEtapa(EtapaFormularioInput entrada, string campo)
    {
        int recusasAntes = _recusas.Count;
        PredicadoDnf? exibicao = LerPredicado(entrada.Exibicao, $"{campo}.exibicao");
        return RecusouDesde(recusasAntes)
            ? null
            : new EtapaDoModelo(
                entrada.Codigo, entrada.Ordem, EstruturaFormulario.TipoDoToken(entrada.Tipo), EstruturaFormulario.BlocoDoToken(entrada.Bloco),
                entrada.Titulo, entrada.Descricao, entrada.Aviso, exibicao);
    }

    private ItemDoModelo? LerItem(FatoColetadoInput entrada, string campo)
    {
        int recusasAntes = _recusas.Count;
        TipoRenderizacao? tipo = LerTipoRenderizacao(entrada.Apresentacao?.TipoRenderizacao, $"{campo}.{FatoColetadoInput.CaminhoDaApresentacao}");
        RegrasDoCampo? regras = LerRegrasDoCampo(entrada.Regras, $"{campo}.{FatoColetadoInput.CaminhoDasRegras}");
        return RecusouDesde(recusasAntes)
            ? null
            : new ItemDoModelo(
                entrada.FatoCodigo,
                entrada.Ordem,
                entrada.EtapaCodigo,
                EntradaDeRegras.Apresentacao(entrada.Apresentacao, tipo!.Value),
                FormatoDoCatalogo(entrada.FatoCodigo),
                regras!);
    }

    private RegrasDoCampo? LerRegrasDoCampo(RegrasDoCampoInput? entrada, string campo)
    {
        PredicadoDnf? exibicao = LerPredicado(entrada?.Exibicao, $"{campo}.exibicao");
        Obrigatoriedade? obrigatoriedade = LerObrigatoriedade(
            entrada?.Obrigatoriedade?.Tipo, entrada?.Obrigatoriedade?.Predicado, $"{campo}.obrigatoriedade", $"{campo}.obrigatoriedade.predicado");
        List<RestricaoValor> restricoes = LerRestricoes(entrada?.Restricoes, campo);
        Impedimento? impedimento = LerImpedimento(entrada?.Impedimento, campo);
        return obrigatoriedade is null ? null : new RegrasDoCampo(exibicao, obrigatoriedade, restricoes, impedimento);
    }

    private TipoRenderizacao? LerTipoRenderizacao(string? codigo, string campo)
    {
        Result<TipoRenderizacao> tipo = TipoRenderizacaoDaEntrada.Ler(codigo);
        if (tipo.IsFailure)
        {
            _recusas.Add(new($"{campo}.tipoRenderizacao", tipo.Error!));
        }

        return tipo.IsSuccess ? tipo.Value : null;
    }

    private TermoDoModelo? LerTermo(TermoExigidoInput entrada, string campo)
    {
        int recusasAntes = _recusas.Count;
        PredicadoDnf? exibicao = LerPredicado(entrada.Exibicao, $"{campo}.exibicao");
        Obrigatoriedade? obrigatoriedade = LerObrigatoriedade(
            entrada.Obrigatoriedade, entrada.PredicadoObrigatoriedade, $"{campo}.obrigatoriedade", $"{campo}.predicadoObrigatoriedade");
        return RecusouDesde(recusasAntes)
            ? null
            : new TermoDoModelo(entrada.Codigo, entrada.Ordem, entrada.TermoId, entrada.VersaoId, exibicao, obrigatoriedade!);
    }

    private GrupoDoModelo? LerGrupo(GrupoColetadoInput entrada, string campo)
    {
        int recusasAntes = _recusas.Count;
        ExibicaoEObrigatoriedade? regras = LerExibicaoEObrigatoriedade(entrada.Regras, $"{campo}.{GrupoColetadoInput.CaminhoDasRegras}");
        List<ItemDoModelo> subitens = [.. Lista(entrada.Subitens, $"{campo}.subitens", LerItem).Select(static subitem => subitem.Parte)];
        return RecusouDesde(recusasAntes)
            ? null
            : new GrupoDoModelo(
                entrada.Codigo, entrada.Ordem, entrada.EtapaCodigo, entrada.Rotulo, EntradaDeRegras.Ocorrencias(entrada.Ocorrencias), regras!, subitens);
    }

    private ExibicaoEObrigatoriedade? LerExibicaoEObrigatoriedade(ExibicaoEObrigatoriedadeInput? entrada, string campo)
    {
        PredicadoDnf? exibicao = LerPredicado(entrada?.Exibicao, $"{campo}.exibicao");
        Obrigatoriedade? obrigatoriedade = LerObrigatoriedade(
            entrada?.Obrigatoriedade?.Tipo, entrada?.Obrigatoriedade?.Predicado, $"{campo}.obrigatoriedade", $"{campo}.obrigatoriedade.predicado");
        return obrigatoriedade is null ? null : new ExibicaoEObrigatoriedade(exibicao, obrigatoriedade);
    }

    /// <summary>
    /// Lê o documento do conjunto padrão pela forma: os códigos de fase e de tipo presentes e as
    /// condições bem formadas. Que a fase exista no cronograma e o tipo no catálogo, e que os fatos
    /// citados sejam do processo, quem confere é a aplicação do modelo, que tem o processo.
    /// </summary>
    private DocumentoExigidoDoModelo? LerDocumento(DocumentoExigidoDoModeloInput entrada, string campo)
    {
        int recusasAntes = _recusas.Count;
        PredicadoDnf? quando = LerPredicado(entrada.Quando, $"{campo}.quando");
        List<BaseLegalDoDocumentoDoModelo> basesLegais = LerBasesLegais(entrada.BasesLegais, campo);
        RecusarCodigosAusentes(entrada, campo);
        ExigenciaPorOpcao? porOpcao = entrada.PorOpcao is { } opcoes ? LerExigenciaPorOpcao(opcoes, campo) : null;
        return RecusouDesde(recusasAntes)
            ? null
            : new DocumentoExigidoDoModelo(
                entrada.FaseCodigo, entrada.TipoDocumentoCodigo, entrada.Obrigatorio, quando, entrada.ConsequenciaIndeferimento, basesLegais, porOpcao);
    }

    private List<BaseLegalDoDocumentoDoModelo> LerBasesLegais(IReadOnlyList<BaseLegalDoModeloInput?>? entradas, string campo)
    {
        IReadOnlyList<BaseLegalDoModeloInput?> bases = entradas ?? [];
        if (bases.Any(static b => b is null))
        {
            RecusarEntradaMalformada($"{campo}.basesLegais", MensagemDaEntradaNula);
        }

        return [.. bases.OfType<BaseLegalDoModeloInput>().Select(static b => new BaseLegalDoDocumentoDoModelo(b.Referencia, b.Abrangencia, b.Status, b.Observacao))];
    }

    private void RecusarCodigosAusentes(DocumentoExigidoDoModeloInput entrada, string campo)
    {
        foreach ((string valor, string nome, string descricao) in new[] { (entrada.FaseCodigo, "faseCodigo", "a fase"), (entrada.TipoDocumentoCodigo, "tipoDocumentoCodigo", "o tipo de documento"), (entrada.ConsequenciaIndeferimento, "consequenciaIndeferimento", "a consequência do indeferimento") })
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                RecusarEntradaMalformada($"{campo}.{nome}", $"O documento exigido do modelo precisa informar {descricao}.");
            }
        }
    }

    private ExigenciaPorOpcao LerExigenciaPorOpcao(ExigenciaPorOpcaoInput entrada, string campo)
    {
        if (string.IsNullOrWhiteSpace(entrada.FatoCodigo))
        {
            RecusarEntradaMalformada($"{campo}.porOpcao.fatoCodigo", "A exigência por opção precisa informar o fato cujas opções a multiplicam.");
        }

        if ((entrada.Exceto ?? []).Any(string.IsNullOrWhiteSpace))
        {
            RecusarEntradaMalformada($"{campo}.porOpcao.exceto", "A opção excetuada precisa ser um código.");
        }

        return new ExigenciaPorOpcao(entrada.FatoCodigo, [.. (entrada.Exceto ?? []).OfType<string>()]);
    }

    private List<RestricaoValor> LerRestricoes(IReadOnlyList<RestricaoValorInput?>? entradas, string campo)
    {
        List<RestricaoValor> restricoes = [];
        foreach ((int indice, RestricaoValorInput? entrada) in (entradas ?? []).Index())
        {
            if (LerRestricao(entrada, $"{campo}.restricoes[{indice}]") is { } restricao)
            {
                restricoes.Add(restricao);
            }
        }

        return restricoes;
    }

    private RestricaoValor? LerRestricao(RestricaoValorInput? entrada, string campo)
    {
        Result<RestricaoValor> restricao = entrada is null
            ? Result<RestricaoValor>.Failure(new DomainError(RestricaoValorErrorCodes.TipoDesconhecido, "A restrição de valor é nula."))
            : EntradaDeRegras.Restricao(entrada);
        if (restricao.IsFailure)
        {
            _recusas.Add(new(campo, restricao.Error!));
        }

        return restricao.Value;
    }

    private Impedimento? LerImpedimento(ImpedimentoInput? entrada, string campo)
    {
        Result<Impedimento?> impedimento = EntradaDeRegras.Impedimento(entrada);
        if (impedimento.IsFailure)
        {
            _recusas.Add(new($"{campo}.impedimento.quando", impedimento.Error!));
        }

        return impedimento.Value;
    }

    private PredicadoDnf? LerPredicado(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? entrada, string campo)
    {
        Result<PredicadoDnf?> predicado = EntradaDeRegras.Predicado(entrada);
        if (predicado.IsFailure)
        {
            _recusas.Add(new(campo, predicado.Error!));
        }

        return predicado.Value;
    }

    private Obrigatoriedade? LerObrigatoriedade(
        string? token, IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? predicadoDeEntrada, string caminhoDoToken, string caminhoDoPredicado)
    {
        int recusasAntes = _recusas.Count;
        PredicadoDnf? predicado = LerPredicado(predicadoDeEntrada, caminhoDoPredicado);
        return RecusouDesde(recusasAntes) ? null : ObrigatoriedadeDoToken(token, predicado, caminhoDoToken);
    }

    private Obrigatoriedade? ObrigatoriedadeDoToken(string? token, PredicadoDnf? predicado, string caminho)
    {
        Obrigatoriedade? obrigatoriedade = EntradaDeRegras.Obrigatoriedade(token, predicado);
        if (obrigatoriedade is null)
        {
            _recusas.Add(new(caminho, new DomainError(
                ModeloFormularioErrorCodes.ObrigatoriedadeInvalida,
                "A obrigatoriedade é SEMPRE ou NUNCA, sem predicado, ou QUANDO, com predicado.")));
        }

        return obrigatoriedade;
    }

    private string? FormatoDoCatalogo(string? fatoCodigo) =>
        fatoCodigo is { } codigo && formatos.TryGetValue(TextoGravavel.CodigoNaFormaGravada(codigo), out string? formato) ? formato : null;

    private void RecusarEntradaMalformada(string campo, string mensagem) =>
        _recusas.Add(new(campo, new DomainError(ModeloFormularioErrorCodes.EntradaMalformada, mensagem)));

    private bool RecusouDesde(int recusasAntes) => _recusas.Count > recusasAntes;
}
