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
/// Uma leitura da entrada do conteúdo do modelo, pela forma das regras do formulário
/// (<see cref="EntradaDeRegras"/>): token desconhecido vira o sentinela, que a forma recusa depois;
/// predicado, obrigatoriedade e restrição malformados são recusados aqui. O formato do campo de texto
/// é o do fato no catálogo. Acumula as recusas, na ordem em que as partes são lidas, e guarda a posição
/// de cada parte lida na lista da entrada. A parte nula ou com recusa não é lida; a recusa dela fica em
/// <see cref="Recusas"/>.
/// </summary>
/// <param name="formatos">O formato do campo de texto de cada fato, pelo código na forma gravada.</param>
internal sealed class LeituraDoConteudo(IReadOnlyDictionary<string, string> formatos)
{
    private const string MensagemDaEntradaNula = "A etapa, o item, o grupo ou o termo veio nulo.";

    private readonly List<FieldError> _recusas = [];

    /// <summary>As recusas da leitura, na ordem em que as partes foram lidas.</summary>
    public IReadOnlyList<FieldError> Recusas => _recusas;

    /// <summary>
    /// Lê as partes na ordem dos argumentos, que é a ordem das recusas: etapas, itens, termos, grupos,
    /// documentos e grupos de documentos.
    /// </summary>
    public ConteudoLido Ler(ConteudoDoModeloInput? entrada) => new(
        entrada?.Titulo,
        Lista(entrada?.Etapas, "etapas", LerEtapa),
        Lista(entrada?.Itens, "itens", LerItem),
        Lista(entrada?.Termos, "termos", LerTermo),
        [.. (entrada?.Pressupostos ?? []).Select(static p => p ?? string.Empty)],
        Lista(entrada?.Grupos, "grupos", LerGrupo),
        LerConjuntoDeDocumentos(entrada?.ConjuntoDeDocumentos));

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
        TipoEtapaFormulario? tipo = LerTipoDaEtapa(entrada.Tipo, $"{campo}.tipo");
        BlocoSistema? bloco = LerBlocoDaEtapa(entrada.Bloco, $"{campo}.bloco");
        return RecusouDesde(recusasAntes)
            ? null
            : new EtapaDoModelo(
                entrada.Codigo, entrada.Ordem, tipo!.Value, bloco,
                entrada.Apresentacao?.Titulo ?? string.Empty, entrada.Apresentacao?.Descricao, entrada.Apresentacao?.Aviso, exibicao);
    }

    private TipoEtapaFormulario? LerTipoDaEtapa(string? codigo, string campo)
    {
        Result<TipoEtapaFormulario> tipo = TipoEBlocoDaEntrada.LerTipo(codigo);
        if (tipo.IsFailure)
        {
            _recusas.Add(new(campo, tipo.Error!));
        }

        return tipo.IsSuccess ? tipo.Value : null;
    }

    private BlocoSistema? LerBlocoDaEtapa(string? codigo, string campo)
    {
        Result<BlocoSistema?> bloco = TipoEBlocoDaEntrada.LerBloco(codigo);
        if (bloco.IsFailure)
        {
            _recusas.Add(new(campo, bloco.Error!));
        }

        return bloco.IsSuccess ? bloco.Value : null;
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
        ExibicaoEObrigatoriedade? regras = LerExibicaoEObrigatoriedade(entrada.Regras, $"{campo}.{TermoExigidoInput.CaminhoDasRegras}");
        return RecusouDesde(recusasAntes)
            ? null
            : new TermoDoModelo(entrada.Codigo, entrada.Ordem, entrada.Termo?.TermoId ?? Guid.Empty, entrada.Termo?.VersaoId ?? Guid.Empty, regras!);
    }

    private GrupoDoModelo? LerGrupo(GrupoColetadoInput entrada, string campo)
    {
        int recusasAntes = _recusas.Count;
        OcorrenciasLidasDoGrupo ocorrencias = OcorrenciasLidasDoGrupo.Ler(entrada.Ocorrencias);
        _recusas.AddRange(ocorrencias.Recusas.Select(recusa => recusa with { Field = $"{campo}.{recusa.Field}" }));
        ExibicaoEObrigatoriedade? regras = LerExibicaoEObrigatoriedade(entrada.Regras, $"{campo}.{GrupoColetadoInput.CaminhoDasRegras}");
        List<ItemDoModelo> subitens = [.. Lista(entrada.Subitens, $"{campo}.subitens", LerItem).Select(static subitem => subitem.Parte)];
        return RecusouDesde(recusasAntes)
            ? null
            : new GrupoDoModelo(
                entrada.Codigo, entrada.Ordem, entrada.EtapaCodigo, entrada.Rotulo, ocorrencias.Declaradas!, regras!, subitens);
    }

    private ExibicaoEObrigatoriedade? LerExibicaoEObrigatoriedade(ExibicaoEObrigatoriedadeInput? entrada, string campo)
    {
        PredicadoDnf? exibicao = LerPredicado(entrada?.Exibicao, $"{campo}.exibicao");
        Obrigatoriedade? obrigatoriedade = LerObrigatoriedade(
            entrada?.Obrigatoriedade?.Tipo, entrada?.Obrigatoriedade?.Predicado, $"{campo}.obrigatoriedade", $"{campo}.obrigatoriedade.predicado");
        return obrigatoriedade is null ? null : new ExibicaoEObrigatoriedade(exibicao, obrigatoriedade);
    }

    private ConjuntoLidoDeDocumentos LerConjuntoDeDocumentos(ConjuntoDeDocumentosDoModeloInput? entrada) => new(
        Lista(entrada?.DocumentosExigidos, $"{ConjuntoDeDocumentosDoModelo.Caminho}.documentosExigidos", LerDocumento),
        Lista(entrada?.GruposDeDocumentos, $"{ConjuntoDeDocumentosDoModelo.Caminho}.gruposDeDocumentos", LerGrupoDeDocumentos));

    /// <summary>
    /// Lê o grupo de documentos: a quantidade mínima fica como veio, a recusa de que ela caiba nos documentos
    /// é do processo, e cada documento é lido como o avulso.
    /// </summary>
    private GrupoLidoDeDocumentos? LerGrupoDeDocumentos(GrupoDeDocumentosDoModeloInput entrada, string campo)
    {
        int recusasAntes = _recusas.Count;
        IndeferimentoDoGrupoDeDocumentosDoModelo? indeferimento = LerIndeferimentoDoGrupo(entrada.Indeferimento, campo);
        List<(int Indice, DocumentoExigidoDoModelo Documento)> documentos = Lista(entrada.Documentos, $"{campo}.documentos", LerDocumento);
        return RecusouDesde(recusasAntes)
            ? null
            : new GrupoLidoDeDocumentos(new GrupoDeDocumentosDoModelo(entrada.QuantidadeMinima, indeferimento, [.. documentos.Select(static d => d.Documento)]), documentos);
    }

    private IndeferimentoDoGrupoDeDocumentosDoModelo? LerIndeferimentoDoGrupo(IndeferimentoDoGrupoDeDocumentosDoModeloInput? entrada, string campo)
    {
        if (entrada is null)
        {
            return null;
        }

        List<BaseLegalDoModelo> basesLegais = LerBasesLegais(entrada.BasesLegais, $"{campo}.indeferimento");
        if (string.IsNullOrWhiteSpace(entrada.Consequencia))
        {
            RecusarEntradaMalformada($"{campo}.indeferimento.consequencia", "O grupo de documentos do modelo precisa informar a consequência do indeferimento.");
        }

        return new IndeferimentoDoGrupoDeDocumentosDoModelo(entrada.Consequencia ?? string.Empty, basesLegais);
    }

    /// <summary>
    /// Lê o documento do conjunto padrão pela forma: os códigos de fase e de tipo presentes, as condições
    /// bem formadas e os códigos dos requisitos conhecidos. Que a fase exista no cronograma e o tipo no
    /// catálogo, e que os fatos citados sejam do processo, quem confere é a aplicação do modelo, que tem o
    /// processo.
    /// </summary>
    private DocumentoExigidoDoModelo? LerDocumento(DocumentoExigidoDoModeloInput entrada, string campo)
    {
        int recusasAntes = _recusas.Count;
        PredicadoDnf? quando = LerPredicado(entrada.Quando, $"{campo}.quando");
        IndeferimentoDoDocumentoDoModelo indeferimento = LerIndeferimento(entrada, campo);
        RequisitosDoDocumentoDoModelo? requisitos = LerRequisitos(entrada.Requisitos, campo);
        ExigenciaPorOpcao? porOpcao = entrada.PorOpcao is { } opcoes ? LerExigenciaPorOpcao(opcoes, campo) : null;
        return RecusouDesde(recusasAntes)
            ? null
            : new DocumentoExigidoDoModelo(entrada.FaseCodigo, entrada.TipoDocumentoCodigo, quando, indeferimento, requisitos!, porOpcao);
    }

    private IndeferimentoDoDocumentoDoModelo LerIndeferimento(DocumentoExigidoDoModeloInput entrada, string campo)
    {
        List<BaseLegalDoModelo> basesLegais = LerBasesLegais(entrada.Indeferimento?.BasesLegais, $"{campo}.indeferimento");
        RecusarCodigosAusentes(entrada, campo);
        return new IndeferimentoDoDocumentoDoModelo(
            entrada.Indeferimento?.Obrigatorio ?? false, entrada.Indeferimento?.Consequencia ?? string.Empty, basesLegais);
    }

    private RequisitosDoDocumentoDoModelo? LerRequisitos(RequisitosDoDocumentoDoModeloInput? entrada, string campo)
    {
        RequisitosLidosDoDocumento requisitos = RequisitosLidosDoDocumento.Ler(entrada);
        _recusas.AddRange(requisitos.Recusas.Select(recusa => recusa with { Field = $"{campo}.{recusa.Field}" }));
        return requisitos.Declarados;
    }

    private List<BaseLegalDoModelo> LerBasesLegais(IReadOnlyList<BaseLegalDoModeloInput?>? entradas, string campo)
    {
        IReadOnlyList<BaseLegalDoModeloInput?> bases = entradas ?? [];
        if (bases.Any(static b => b is null))
        {
            RecusarEntradaMalformada($"{campo}.basesLegais", MensagemDaEntradaNula);
        }

        return [.. bases.OfType<BaseLegalDoModeloInput>().Select(static b => new BaseLegalDoModelo(b.Referencia, b.Abrangencia, b.Situacao, b.Observacao))];
    }

    private void RecusarCodigosAusentes(DocumentoExigidoDoModeloInput entrada, string campo)
    {
        (string? Valor, string Caminho, string Descricao)[] codigos =
        [
            (entrada.FaseCodigo, "faseCodigo", "a fase"),
            (entrada.TipoDocumentoCodigo, "tipoDocumentoCodigo", "o tipo de documento"),
            (entrada.Indeferimento?.Consequencia, "indeferimento.consequencia", "a consequência do indeferimento"),
        ];
        foreach ((_, string caminho, string descricao) in codigos.Where(static c => string.IsNullOrWhiteSpace(c.Valor)))
        {
            RecusarEntradaMalformada($"{campo}.{caminho}", $"O documento exigido do modelo precisa informar {descricao}.");
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
            _recusas.AddRange(impedimento.Errors.Select(e => new FieldError($"{campo}.impedimento.quando{e.Field}", e.Error)));
        }

        return impedimento.Value;
    }

    private PredicadoDnf? LerPredicado(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? entrada, string campo)
    {
        Result<PredicadoDnf?> predicado = EntradaDeRegras.Predicado(entrada);
        if (predicado.IsFailure)
        {
            _recusas.AddRange(predicado.Errors.Select(e => new FieldError($"{campo}{e.Field}", e.Error)));
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
