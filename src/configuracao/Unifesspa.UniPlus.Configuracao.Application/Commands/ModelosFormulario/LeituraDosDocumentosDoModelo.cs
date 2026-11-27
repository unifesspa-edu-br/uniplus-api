namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

internal sealed class LeituraDosDocumentosDoModelo(RecusasDaLeituraDoModelo _recusas)
{
    private const string MensagemDoReenvio = "Informe se o indeferimento abre prazo para reenvio.";

    public ConjuntoLidoDeDocumentos Ler(ConjuntoDeDocumentosDoModeloInput? entrada) => new(
        _recusas.Lista(entrada?.DocumentosExigidos, $"{ConjuntoDeDocumentosDoModelo.Caminho}.documentosExigidos", LerDocumento),
        _recusas.Lista(entrada?.GruposDeDocumentos, $"{ConjuntoDeDocumentosDoModelo.Caminho}.gruposDeDocumentos", LerGrupoDeDocumentos));

    /// <summary>
    /// Lê o grupo de documentos: a quantidade mínima fica como veio, a recusa de que ela caiba nos documentos
    /// é do processo, e cada documento é lido como o avulso.
    /// </summary>
    private GrupoLidoDeDocumentos? LerGrupoDeDocumentos(GrupoDeDocumentosDoModeloInput entrada, string campo)
    {
        int recusasAntes = _recusas.Recusas.Count;
        IndeferimentoDoGrupoDeDocumentosDoModelo? indeferimento = LerIndeferimentoDoGrupo(entrada, campo);
        List<(int Indice, DocumentoExigidoDoModelo Documento)> documentos = _recusas.Lista(entrada.Documentos, $"{campo}.documentos", LerDocumento);
        return _recusas.RecusouDesde(recusasAntes)
            ? null
            : new GrupoLidoDeDocumentos(new GrupoDeDocumentosDoModelo(entrada.QuantidadeMinima, indeferimento, [.. documentos.Select(static d => d.Documento)]), documentos);
    }

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
        RequisitosDoDocumentoDoModelo? requisitos = LerRequisitos(entrada.Requisitos, campo);
        ExigenciaPorOpcao? porOpcao = entrada.PorOpcao is { } opcoes ? LerExigenciaPorOpcao(opcoes, campo) : null;
        return _recusas.RecusouDesde(recusasAntes)
            ? null
            : new DocumentoExigidoDoModelo(entrada.FaseCodigo, entrada.TipoDocumentoCodigo, quando, indeferimento, requisitos!, porOpcao);
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

    private RequisitosDoDocumentoDoModelo? LerRequisitos(RequisitosDoDocumentoDoModeloInput? entrada, string campo)
    {
        RequisitosLidosDoDocumento requisitos = RequisitosLidosDoDocumento.Ler(entrada);
        _recusas.Recusas.AddRange(requisitos.Recusas.Select(recusa => recusa.NoCaminhoDoItem(campo)));
        return requisitos.Declarados;
    }

    private List<BaseLegalDoModelo> LerBasesLegais(IReadOnlyList<BaseLegalDoModeloInput?>? entradas, string campo)
    {
        IReadOnlyList<BaseLegalDoModeloInput?> bases = entradas ?? [];
        if (bases.Any(static b => b is null))
        {
            _recusas.RecusarEntradaMalformada($"{campo}.basesLegais", RecusasDaLeituraDoModelo.MensagemDaEntradaNula);
        }

        ConferirBasesLegais(bases, campo);

        return [.. bases.OfType<BaseLegalDoModeloInput>().Select(static b => new BaseLegalDoModelo(b.Referencia, b.Abrangencia, b.Situacao, b.Observacao))];
    }

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
