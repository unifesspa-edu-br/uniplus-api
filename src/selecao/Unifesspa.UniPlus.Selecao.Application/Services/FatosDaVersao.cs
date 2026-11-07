namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Um grupo repetível do formulário congelado, com os fatos dos campos dele e se o próprio candidato é um dos membros.</summary>
internal sealed record GrupoDoProcesso(
    string Codigo, string Rotulo, string? Secao, int Ordem, IReadOnlyDictionary<string, FatoDoProcesso> Subitens, bool IncluiCandidato);

/// <summary>Um campo identificador do formulário congelado: o fato e o grupo repetível que o abriga, quando há.</summary>
internal sealed record CampoIdentificador(string FatoCodigo, string? GrupoCodigo, bool EhCpf);

/// <summary>
/// Os fatos da inscrição como a versão do processo os congelou: os coletados (fora e dentro de grupo) e os derivados,
/// e o título que cada seção do formulário de inscrição tem na versão.
/// Lê só o envelope congelado, nunca o catálogo vivo (a Seleção congela por cópia na publicação).
/// </summary>
internal sealed record FatosDaVersao(
    IReadOnlyDictionary<string, FatoDoProcesso> Coletados,
    IReadOnlyList<GrupoDoProcesso> Grupos,
    IReadOnlyDictionary<string, FatoDoProcesso> Derivados,
    IReadOnlyDictionary<string, string> TitulosDasSecoes)
{
    /// <summary>Extrai os fatos da finalidade Inscrição do envelope reidratado.</summary>
    public static FatosDaVersao De(EnvelopeReidratado envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        Dictionary<string, Dictionary<string, string>> declaradas = OpcoesDeclaradasPorFato(envelope);
        Dictionary<string, FatoDoProcesso> derivados = envelope.FatosDerivadosCongelados.ToDictionary(
            static d => d.Key, d => Derivado(d.Value, envelope), StringComparer.Ordinal);

        return new FatosDaVersao(
            ColetadosForaDeGrupo(envelope, declaradas), GruposDaInscricao(envelope, declaradas), derivados, TitulosDasEtapas(envelope));
    }

    private static Dictionary<string, Dictionary<string, string>> OpcoesDeclaradasPorFato(EnvelopeReidratado envelope)
    {
        Dictionary<string, Dictionary<string, string>> doProcesso = [];
        foreach (OpcaoDeclaradaFato opcao in envelope.Grafo.OpcoesDeclaradas)
        {
            if (!doProcesso.TryGetValue(opcao.FatoCodigo, out Dictionary<string, string>? porCodigo))
            {
                doProcesso[opcao.FatoCodigo] = porCodigo = new Dictionary<string, string>(StringComparer.Ordinal);
            }

            porCodigo[opcao.Codigo] = opcao.Rotulo;
        }

        return doProcesso;
    }

    private static FatoDoProcesso Coletado(
        FatoColetado fato, Dictionary<string, Dictionary<string, string>> declaradas, EnvelopeReidratado envelope) => new(
        fato.FatoCodigo,
        fato.Rotulo,
        fato.Descricao,
        TiposDeItem.De(fato.TipoRenderizacao),
        ComponentesDeItem.De(
            fato.ComponenteExibicao,
            EhCpf(fato.FatoCodigo, fato.Formato),
            Classificacao(fato.ClassificacaoProtecao) == ClassificacaoDeProtecao.Identificador),
        Classificacao(fato.ClassificacaoProtecao),
        string.IsNullOrWhiteSpace(fato.HipoteseLegal) ? null : fato.HipoteseLegal,
        fato.EtapaCodigo,
        fato.Ordem,
        fato.OrigemValores == OrigemValoresColeta.UnidadesFederativas ? NomesDasUfs() : Opcoes(fato.FatoCodigo, declaradas, envelope),
        EhCpf(fato.FatoCodigo, fato.Formato));

    private static Dictionary<string, FatoDoProcesso> ColetadosForaDeGrupo(
        EnvelopeReidratado envelope, Dictionary<string, Dictionary<string, string>> declaradas) =>
        envelope.Grafo.FatosColetados
            .Where(static f => f.Finalidade == FinalidadeFormulario.Inscricao && f.GrupoColetadoId is null)
            .GroupBy(static f => f.FatoCodigo, StringComparer.Ordinal)
            .ToDictionary(static g => g.Key, g => Coletado(g.First(), declaradas, envelope), StringComparer.Ordinal);

    private static List<GrupoDoProcesso> GruposDaInscricao(
        EnvelopeReidratado envelope, Dictionary<string, Dictionary<string, string>> declaradas) =>
    [
        .. envelope.Grafo.GruposColetados
            .Where(static g => g.Finalidade == FinalidadeFormulario.Inscricao)
            .Select(g => new GrupoDoProcesso(
                g.Codigo, g.Rotulo, g.EtapaCodigo, g.Ordem,
                g.Subitens.GroupBy(static s => s.FatoCodigo, StringComparer.Ordinal)
                    .ToDictionary(static s => s.Key, s => Coletado(s.First(), declaradas, envelope), StringComparer.Ordinal),
                g.IncluiCandidato)),
    ];

    private static Dictionary<string, string> TitulosDasEtapas(EnvelopeReidratado envelope) =>
        envelope.Grafo.Formularios
            .Where(static f => f.Finalidade == FinalidadeFormulario.Inscricao)
            .SelectMany(static f => f.Etapas)
            .ToDictionary(static e => e.Codigo, static e => e.Titulo, StringComparer.Ordinal);

    /// <summary>
    /// Os campos identificadores de código <paramref name="fatoCodigo"/>: com <paramref name="emGrupo"/>, um por grupo
    /// repetível que o contém; sem ele, só o de fora de grupo. Quem pede a ocorrência de um grupo nunca recebe o campo da
    /// raiz, e quem não a pede nunca recebe o de grupo. Vazio quando o fato não existe na versão ou não é classificado como
    /// identificador. Só o campo assim
    /// classificado se revela, e a resposta que o guarda é a cifrada com a chave dos identificadores: o handler não decide de
    /// onde o valor vem. O mesmo fato pode estar em mais de um grupo; quem localiza a ocorrência escolhe o grupo.
    /// </summary>
    public IReadOnlyList<CampoIdentificador> Identificadores(string fatoCodigo, bool emGrupo)
    {
        if (!emGrupo)
        {
            return Coletados.TryGetValue(fatoCodigo, out FatoDoProcesso? fora) && fora.Classificacao == ClassificacaoDeProtecao.Identificador
                ? [new CampoIdentificador(fatoCodigo, null, fora.EhCpf)]
                : [];
        }

        return
        [
            .. Grupos
                .Where(g => g.Subitens.TryGetValue(fatoCodigo, out FatoDoProcesso? membro) && membro.Classificacao == ClassificacaoDeProtecao.Identificador)
                .Select(g => new CampoIdentificador(fatoCodigo, g.Codigo, g.Subitens[fatoCodigo].EhCpf)),
        ];
    }

    /// <summary>
    /// A classificação mais alta que a versão congelou para o nome exibido (nome civil e nome social). A classificação que
    /// não se resolve, ou a versão que não coleta nome algum, vale como sensível: na dúvida, a permissão mais restrita.
    /// </summary>
    public ClassificacaoDeProtecao ClassificacaoDoNome()
    {
        ClassificacaoDeProtecao[] congeladas =
        [
            .. new[] { ConjuntoBasicoDaInscricao.Nome, ConjuntoBasicoDaInscricao.NomeSocial }
                .Where(Coletados.ContainsKey)
                .Select(codigo => Coletados[codigo].Classificacao)
                .Select(ClassificacoesDeProtecao.Resolvida),
        ];

        return congeladas.Length == 0 ? ClassificacaoDeProtecao.Sensivel : congeladas.Max();
    }

    /// <summary>
    /// A base legal que a versão congelou para os campos de nome que coleta, as distintas separadas por ponto e vírgula.
    /// Nula quando algum campo de nome não a tem, ou quando a versão não coleta nome algum: na dúvida, o nome sensível não
    /// sai.
    /// </summary>
    public string? BaseLegalDoNome()
    {
        FatoDoProcesso[] nomes =
        [
            .. new[] { ConjuntoBasicoDaInscricao.Nome, ConjuntoBasicoDaInscricao.NomeSocial }
                .Where(Coletados.ContainsKey)
                .Select(codigo => Coletados[codigo]),
        ];

        return nomes.Length > 0 && nomes.All(static n => !string.IsNullOrWhiteSpace(n.BaseLegal))
            ? string.Join("; ", nomes.Select(static n => n.BaseLegal).Distinct(StringComparer.Ordinal))
            : null;
    }

    private static FatoDoProcesso Derivado(FatoDerivadoCongelado derivado, EnvelopeReidratado envelope) =>
        new(
            derivado.Codigo,
            derivado.Nome,
            derivado.Descricao,
            TiposDeItem.De(derivado.Dominio, derivado.Cardinalidade),
            ComponentesDeItem.De(derivado.ComponenteExibicao, formatoCpf: false, identificador: false),
            Classificacao(derivado.ClassificacaoProtecao),
            string.IsNullOrWhiteSpace(derivado.HipoteseLegal) ? null : derivado.HipoteseLegal,
            Secao: null,
            Ordem: 0,
            OpcoesDoDerivado(derivado.Codigo, envelope),
            EhCpf: false);

    /// <summary>A UF de residência sai pelo nome, como a UF coletada; os demais derivados, pelo vocabulário que a versão congelou.</summary>
    private static Dictionary<string, string> OpcoesDoDerivado(string codigo, EnvelopeReidratado envelope) =>
        string.Equals(codigo, DerivadosDoSistemaInscricao.UfDeResidencia, StringComparison.Ordinal)
            ? NomesDasUfs()
            : DescricoesDoDominio(codigo, envelope);

    /// <summary>
    /// O rótulo de cada UF, pela lista fixa do Kernel e não pelo que a versão congelou: a UF sai sempre pelo nome, venha o
    /// valor como sigla ou como código IBGE.
    /// </summary>
    private static Dictionary<string, string> NomesDasUfs()
    {
        Dictionary<string, string> nomes = new(StringComparer.Ordinal);
        foreach (UnidadeFederativa uf in UnidadesFederativas.Todas)
        {
            nomes[uf.Sigla] = uf.Nome;
            nomes[uf.CodigoIbge] = uf.Nome;
        }

        return nomes;
    }

    private static Dictionary<string, string> Opcoes(
        string fatoCodigo, Dictionary<string, Dictionary<string, string>> doProcesso, EnvelopeReidratado envelope)
    {
        Dictionary<string, string> opcoes = new(DescricoesDoDominio(fatoCodigo, envelope), StringComparer.Ordinal);
        if (doProcesso.TryGetValue(fatoCodigo, out Dictionary<string, string>? declaradas))
        {
            foreach ((string codigo, string rotulo) in declaradas)
            {
                opcoes[codigo] = rotulo;
            }
        }

        return opcoes;
    }

    private static Dictionary<string, string> DescricoesDoDominio(string fatoCodigo, EnvelopeReidratado envelope)
    {
        Dictionary<string, string> descricoes = new(StringComparer.Ordinal);
        if (envelope.ValoresSelecionaveisCongelados?.GetValueOrDefault(fatoCodigo) is { } valores)
        {
            foreach (ValorDominioDeclaradoCongelado valor in valores)
            {
                descricoes[valor.Codigo] = valor.Descricao ?? valor.Codigo;
            }
        }

        if (envelope.MetadadosFatosCongelados?.GetValueOrDefault(fatoCodigo)?.ValoresDominioDeclarados is { } declarados)
        {
            foreach (ValorDominioDeclaradoCongelado valor in declarados)
            {
                descricoes.TryAdd(valor.Codigo, valor.Descricao ?? valor.Codigo);
            }
        }

        return descricoes;
    }

    private static bool EhCpf(string fatoCodigo, string? formato) =>
        string.Equals(fatoCodigo, ConjuntoBasicoDaInscricao.Cpf, StringComparison.Ordinal)
        || string.Equals(formato, FormatoDeTexto.FormatoCpf, StringComparison.Ordinal);

    private static ClassificacaoDeProtecao Classificacao(string? token) =>
        ClassificacoesDeProtecao.TryAnalisar(token, out ClassificacaoDeProtecao classificacao) ? classificacao : ClassificacaoDeProtecao.Nenhuma;
}
