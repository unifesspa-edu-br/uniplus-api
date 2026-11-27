namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

internal sealed class LeituraDosDocumentosDoModelo(RecusasDaLeituraDoModelo _recusas)
{
    private const string MensagemDoReenvio = "Informe se o indeferimento abre prazo para reenvio.";

    private readonly List<DocumentoDeGrupoNaoLido> _documentosDeGruposNaoLidos = [];

    public ConjuntoLidoDeDocumentos Ler(ConjuntoDeDocumentosDoModeloInput? entrada) => new(
        _recusas.Lista(entrada?.DocumentosExigidos, ConjuntoDeDocumentosDoModelo.ListaDosDocumentos, LerDocumento),
        _recusas.ListaComPosicao(entrada?.GruposDeDocumentos, ConjuntoDeDocumentosDoModelo.ListaDosGrupos, LerGrupoDeDocumentos),
        _documentosDeGruposNaoLidos);

    /// <summary>
    /// Lê o grupo de documentos: a quantidade mínima fica como veio, a recusa de que ela caiba nos documentos
    /// é do processo, e cada documento é lido como o avulso.
    /// </summary>
    private GrupoLidoDeDocumentos? LerGrupoDeDocumentos(GrupoDeDocumentosDoModeloInput entrada, int posicao, string campo)
    {
        int recusasAntes = _recusas.Recusas.Count;
        IndeferimentoDoGrupoDeDocumentosDoModelo? indeferimento = LerIndeferimentoDoGrupo(entrada, campo);
        int antesDosDocumentos = _recusas.Recusas.Count;
        DocumentosLidosDoGrupo documentos = LerDocumentosDoGrupo(entrada, campo);
        if (_recusas.RecusouDesde(recusasAntes))
        {
            RecusarGrupoNaoLido(entrada, posicao, documentos, antesDosDocumentos);
            return null;
        }

        return new GrupoLidoDeDocumentos(
            new GrupoDeDocumentosDoModelo(entrada.QuantidadeMinima, indeferimento, [.. documentos.Lidos.Select(static d => d.Documento)]), documentos.Lidos);
    }

    /// <summary>
    /// Lê os documentos do grupo e guarda, na ordem do pedido, as recusas de cada um: as da leitura, dos textos e do formato
    /// dos códigos, se não se leu, ou as dos textos e do formato, se se leu.
    /// </summary>
    private DocumentosLidosDoGrupo LerDocumentosDoGrupo(GrupoDeDocumentosDoModeloInput entrada, string campo)
    {
        List<(int Indice, DocumentoExigidoDoModelo Documento)> lidos = [];
        List<FieldError> recusasNaOrdem = [];
        foreach ((int indice, DocumentoExigidoDoModeloInput? documento) in (entrada.Documentos ?? []).Index())
        {
            (DocumentoExigidoDoModelo? lido, List<FieldError> doDocumento) = LerDocumentoDoGrupo(documento, $"{campo}.documentos[{indice}]");
            if (lido is not null)
            {
                lidos.Add((indice, lido));
            }

            recusasNaOrdem.AddRange(doDocumento);
        }

        return new DocumentosLidosDoGrupo(lidos, recusasNaOrdem);
    }

    private (DocumentoExigidoDoModelo? Documento, List<FieldError> Recusas) LerDocumentoDoGrupo(DocumentoExigidoDoModeloInput? documento, string caminho)
    {
        int antes = _recusas.Recusas.Count;
        DocumentoExigidoDoModelo? lido = _recusas.LerPresente(documento, caminho, LerDocumento);
        return (lido, lido is null ? _recusas.Recusas.GetRange(antes, _recusas.Recusas.Count - antes) : [.. RecusasDoDocumentoLido(lido, caminho)]);
    }

    /// <summary>
    /// Recusa os textos do grupo e, na ordem do pedido, as de cada documento: o domínio não verá o grupo. Os documentos que
    /// se leu seguem para a conferência no catálogo.
    /// </summary>
    private void RecusarGrupoNaoLido(GrupoDeDocumentosDoModeloInput entrada, int posicao, DocumentosLidosDoGrupo documentos, int posicaoDosDocumentos)
    {
        string campo = ConjuntoDeDocumentosDoModelo.CaminhoDoGrupo(posicao);
        _recusas.Recusas.RemoveRange(posicaoDosDocumentos, _recusas.Recusas.Count - posicaoDosDocumentos);
        IEnumerable<(string Campo, string? Texto)> textosDoGrupo = entrada.Indeferimento is { } indeferimento
            ? TextosDoConteudoDoModelo.DoIndeferimento(campo, indeferimento.ConsequenciaDoDescumprimento, BasesNaPosicaoDoPedido(indeferimento.BasesLegais))
            : [];
        _recusas.Recusas.AddRange(RecusasDaLeituraDoModelo.TextosNaoGravaveis(textosDoGrupo));
        _recusas.Recusas.AddRange(documentos.RecusasNaOrdem);
        _documentosDeGruposNaoLidos.AddRange(documentos.Lidos.Select(d => new DocumentoDeGrupoNaoLido(posicao, d.Indice, d.Documento)));
    }

    private static IEnumerable<FieldError> RecusasDoDocumentoLido(DocumentoExigidoDoModelo documento, string caminho) =>
        RecusasDaLeituraDoModelo.TextosNaoGravaveis(TextosDoConteudoDoModelo.DoDocumentoLido(caminho, documento))
            .Concat(FormatoDosCodigosDoConteudo.ConferirDocumento(documento, caminho));

    private IndeferimentoDoGrupoDeDocumentosDoModelo? LerIndeferimentoDoGrupo(GrupoDeDocumentosDoModeloInput grupo, string campo)
    {
        if (grupo.Indeferimento is not { } entrada)
        {
            return null;
        }

        List<BaseLegalDoModelo> basesLegais = LerBasesLegais(entrada.BasesLegais, $"{campo}.indeferimento");
        LerConsequencia(entrada.ConsequenciaDoDescumprimento, $"{campo}.indeferimento.consequenciaDoDescumprimento");
        bool abrePrazo = grupo.Documentos is { Count: > 0 }
            ? Informado(entrada.AbrePrazoParaReenvio, $"{campo}.indeferimento.abrePrazoParaReenvio", MensagemDoReenvio)
            : entrada.AbrePrazoParaReenvio is true;

        return new IndeferimentoDoGrupoDeDocumentosDoModelo(entrada.ConsequenciaDoDescumprimento, basesLegais, abrePrazo);
    }

    /// <summary>
    /// Lê o documento do conjunto padrão pela forma: os códigos de fase e de tipo presentes, as condições
    /// bem formadas e os códigos dos requisitos conhecidos. Que a fase exista no cronograma e o tipo no
    /// catálogo, e que os fatos citados sejam do processo, quem confere é a aplicação do modelo, que tem o
    /// processo.
    /// </summary>
    private DocumentoExigidoDoModelo? LerDocumento(DocumentoExigidoDoModeloInput entrada, string campo)
    {
        int recusasAntes = _recusas.Recusas.Count;
        PredicadoDnf? quando = _recusas.LerPredicado(entrada.Quando, $"{campo}.quando");
        IndeferimentoDoDocumentoDoModelo indeferimento = LerIndeferimento(entrada, campo);
        RequisitosLidosDoDocumento requisitos = LerRequisitos(entrada.Requisitos, campo);
        ExigenciaPorOpcao? porOpcao = entrada.PorOpcao is { } opcoes ? LerExigenciaPorOpcao(opcoes, campo) : null;
        if (_recusas.RecusouDesde(recusasAntes))
        {
            RecusarDocumentoNaoLido(entrada, campo, quando, requisitos);
            return null;
        }

        return new DocumentoExigidoDoModelo(entrada.FaseCodigo, entrada.TipoDocumentoCodigo, quando, indeferimento, requisitos.Declarados!, porOpcao);
    }

    /// <summary>
    /// Recusa os textos e o formato dos códigos do documento que a leitura recusou, com o que se leu dele e nas posições do
    /// pedido: o código ausente já foi recusado, e o texto da condição malformada não conta.
    /// </summary>
    private void RecusarDocumentoNaoLido(
        DocumentoExigidoDoModeloInput entrada, string campo, PredicadoDnf? quando, RequisitosLidosDoDocumento requisitos)
    {
        List<(string Campo, string? Texto)> referencias = [.. requisitos.ReferenciasInformadas.Select(r => ($"{campo}.{r.Campo}", r.Texto))];
        (string? fatoPorOpcao, IReadOnlyList<string?>? exceto) = entrada.PorOpcao is { } porOpcao
            ? (porOpcao.FatoCodigo, porOpcao.Exceto ?? [])
            : (null, null);
        IEnumerable<(string Campo, string? Texto)> opcoes = exceto is null
            ? []
            : TextosDoConteudoDoModelo.DaExigenciaPorOpcao(campo, fatoPorOpcao, exceto);
        IEnumerable<(string Campo, string? Texto)> indeferimento = TextosDoConteudoDoModelo.DoIndeferimento(
            campo, entrada.Indeferimento?.ConsequenciaDoDescumprimento, BasesNaPosicaoDoPedido(entrada.Indeferimento?.BasesLegais));
        _recusas.Recusas.AddRange(RecusasDaLeituraDoModelo.TextosNaoGravaveis(TextosDoConteudoDoModelo.DoDocumento(
            campo, entrada.FaseCodigo, entrada.TipoDocumentoCodigo, quando, referencias, opcoes, indeferimento)));
        _recusas.Recusas.AddRange(FormatoDosCodigosDoConteudo.ConferirCodigosInformados(
            entrada.FaseCodigo, entrada.TipoDocumentoCodigo, referencias, fatoPorOpcao, exceto, campo));
    }

    private IndeferimentoDoDocumentoDoModelo LerIndeferimento(DocumentoExigidoDoModeloInput entrada, string campo)
    {
        string caminho = $"{campo}.indeferimento";
        IndeferimentoDoDocumentoDoModeloInput? bloco = entrada.Indeferimento;
        List<BaseLegalDoModelo> basesLegais = LerBasesLegais(bloco?.BasesLegais, caminho);
        RecusarCodigosAusentes(entrada, campo);
        LerConsequencia(bloco?.ConsequenciaDoDescumprimento, $"{caminho}.consequenciaDoDescumprimento");
        bool obrigatorio = Informado(bloco?.Obrigatorio, $"{caminho}.obrigatorio", "Informe se o documento é obrigatório.");
        bool abrePrazo = Informado(bloco?.AbrePrazoParaReenvio, $"{caminho}.abrePrazoParaReenvio", MensagemDoReenvio);
        return new IndeferimentoDoDocumentoDoModelo(obrigatorio, bloco?.ConsequenciaDoDescumprimento, basesLegais, abrePrazo);
    }

    private bool Informado(bool? valor, string campo, string mensagem)
    {
        if (valor is { } informado)
        {
            return informado;
        }

        _recusas.RecusarEntradaMalformada(campo, mensagem);
        return false;
    }

    private RequisitosLidosDoDocumento LerRequisitos(RequisitosDoDocumentoDoModeloInput? entrada, string campo)
    {
        RequisitosLidosDoDocumento requisitos = RequisitosLidosDoDocumento.Ler(entrada);
        _recusas.Recusas.AddRange(requisitos.Recusas.Select(recusa => recusa.NoCaminhoDoItem(campo)));
        return requisitos;
    }

    private List<BaseLegalDoModelo> LerBasesLegais(IReadOnlyList<BaseLegalDoModeloInput?>? entradas, string campo)
    {
        IReadOnlyList<BaseLegalDoModeloInput?> bases = entradas ?? [];
        if (bases.Any(static b => b is null))
        {
            _recusas.RecusarEntradaMalformada($"{campo}.basesLegais", RecusasDaLeituraDoModelo.MensagemDaEntradaNula);
        }

        ConferirBasesLegais(bases, campo);

        return [.. BasesNaPosicaoDoPedido(bases).OfType<BaseLegalDoModelo>()];
    }

    private static IReadOnlyList<BaseLegalDoModelo?> BasesNaPosicaoDoPedido(IReadOnlyList<BaseLegalDoModeloInput?>? entradas) =>
        [.. (entradas ?? []).Select(static b => b is null ? null : new BaseLegalDoModelo(b.Referencia, b.Abrangencia, b.Situacao, b.Observacao))];

    private void ConferirBasesLegais(IReadOnlyList<BaseLegalDoModeloInput?> bases, string campo)
    {
        foreach ((int indice, BaseLegalDoModeloInput? item) in bases.Index())
        {
            if (item is not null)
            {
                ConferirCodigo<TipoAbrangencia>(item.Abrangencia, $"{campo}.basesLegais[{indice}].abrangencia", ModeloFormularioErrorCodes.AbrangenciaDesconhecida, "A abrangência da base legal é desconhecida.");
                ConferirCodigo<StatusBaseLegal>(item.Situacao, $"{campo}.basesLegais[{indice}].situacao", ModeloFormularioErrorCodes.SituacaoDaBaseLegalDesconhecida, "A situação da base legal é desconhecida.");
            }
        }

    }

    private void LerConsequencia(string? consequenciaDoDescumprimento, string campo)
    {
        if (consequenciaDoDescumprimento is not null)
        {
            ConferirCodigo<ConsequenciaDoDescumprimento>(consequenciaDoDescumprimento, campo,
                ModeloFormularioErrorCodes.ConsequenciaDoDescumprimentoDesconhecida, "A consequência do descumprimento do modelo de formulário não é uma das consequências cadastradas.");
        }
    }

    private void ConferirCodigo<TEnum>(string? codigo, string campo, string erro, string mensagem)
        where TEnum : struct, Enum
    {
        if (!CatalogoDe<TEnum>.TentarObter(codigo, out _))
        {
            _recusas.Recusar(campo, new DomainError(erro, mensagem));
        }
    }

    private void RecusarCodigosAusentes(DocumentoExigidoDoModeloInput entrada, string campo)
    {
        (string? Valor, string Caminho, string Descricao)[] codigos =
        [
            (entrada.FaseCodigo, "faseCodigo", "a fase"),
            (entrada.TipoDocumentoCodigo, "tipoDocumentoCodigo", "o tipo de documento"),
        ];
        foreach ((_, string caminho, string descricao) in codigos.Where(static c => string.IsNullOrWhiteSpace(c.Valor)))
        {
            _recusas.RecusarEntradaMalformada($"{campo}.{caminho}", $"O documento exigido do modelo precisa informar {descricao}.");
        }
    }

    private ExigenciaPorOpcao LerExigenciaPorOpcao(ExigenciaPorOpcaoInput entrada, string campo)
    {
        if (string.IsNullOrWhiteSpace(entrada.FatoCodigo))
        {
            _recusas.RecusarEntradaMalformada($"{campo}.porOpcao.fatoCodigo", "A exigência por opção precisa informar o fato cujas opções a multiplicam.");
        }

        if ((entrada.Exceto ?? []).Any(string.IsNullOrWhiteSpace))
        {
            _recusas.RecusarEntradaMalformada($"{campo}.porOpcao.exceto", "A opção excetuada precisa ser um código.");
        }

        return new ExigenciaPorOpcao(entrada.FatoCodigo, [.. (entrada.Exceto ?? []).OfType<string>()]);
    }

}

/// <summary>Os documentos de um grupo que a leitura aceitou, com a posição de cada um, e as recusas de todos, na ordem do pedido.</summary>
internal sealed record DocumentosLidosDoGrupo(List<(int Indice, DocumentoExigidoDoModelo Documento)> Lidos, List<FieldError> RecusasNaOrdem);
