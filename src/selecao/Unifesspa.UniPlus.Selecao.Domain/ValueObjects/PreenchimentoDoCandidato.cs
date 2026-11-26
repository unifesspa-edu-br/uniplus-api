namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Collections.ObjectModel;
using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

public sealed class PreenchimentoDoCandidato : IEquatable<PreenchimentoDoCandidato>
{
    private PreenchimentoDoCandidato(OpcoesDeCursoDaInscricao opcoes, IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos, IReadOnlyList<AceiteDeTermo> aceites, bool copiar = true)
    {
        Opcoes = opcoes;
        Respostas = copiar ? CopiarRespostas(respostas) : respostas;
        Grupos = copiar ? CopiarGrupos(grupos) : grupos;
        Aceites = copiar ? Array.AsReadOnly(aceites.ToArray()) : aceites;
    }

    public OpcoesDeCursoDaInscricao Opcoes { get; }
    public IReadOnlyDictionary<string, JsonElement> Respostas { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> Grupos { get; }
    public IReadOnlyList<AceiteDeTermo> Aceites { get; }

    public static PreenchimentoDoCandidato Criar(OpcoesDeCursoDaInscricao opcoes, IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos, IReadOnlyList<AceiteDeTermo> aceites)
    {
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(respostas);
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(aceites);
        return new(opcoes, respostas, grupos, aceites);
    }

    /// <summary>Recebe o corpo sem copiá-lo antes da conferência de forma; valores recusados podem nem admitir cópia.</summary>
    public static PreenchimentoDoCandidato ParaValidacao(OpcoesDeCursoDaInscricao opcoes, IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos, IReadOnlyList<AceiteDeTermo> aceites) =>
        new(opcoes, respostas, grupos, aceites, copiar: false);

    private static ReadOnlyDictionary<string, JsonElement> CopiarRespostas(IReadOnlyDictionary<string, JsonElement> respostas) =>
        new ReadOnlyDictionary<string, JsonElement>(respostas.ToDictionary(static r => r.Key, static r => r.Value.Clone(), StringComparer.Ordinal));

    private static ReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> CopiarGrupos(
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos) =>
        new ReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(grupos.ToDictionary(
            static g => g.Key, static g => (IReadOnlyList<OcorrenciaDeGrupoInscricao>)CopiarOcorrencias(g.Value)!, StringComparer.Ordinal));

    private static ReadOnlyCollection<OcorrenciaDeGrupoInscricao>? CopiarOcorrencias(IReadOnlyList<OcorrenciaDeGrupoInscricao> ocorrencias) =>
        ocorrencias is null ? null : Array.AsReadOnly(ocorrencias.Select(static o =>
            o is null ? null! : new OcorrenciaDeGrupoInscricao(o.Id, o.Respostas is null ? null! : CopiarRespostas(o.Respostas))).ToArray());

    public PreenchimentoDoCandidato ComOpcoes(OpcoesDeCursoDaInscricao opcoes) => Criar(opcoes, Respostas, Grupos, Aceites);
    public PreenchimentoDoCandidato ComRespostas(IReadOnlyDictionary<string, JsonElement> respostas) => Criar(Opcoes, respostas, Grupos, Aceites);
    public PreenchimentoDoCandidato ComGrupos(IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos) => Criar(Opcoes, Respostas, grupos, Aceites);
    public PreenchimentoDoCandidato ComAceites(IReadOnlyList<AceiteDeTermo> aceites) => Criar(Opcoes, Respostas, Grupos, aceites);
    public IReadOnlyDictionary<string, JsonElement> RespostasAposMesclar(IReadOnlyDictionary<string, JsonElement> recebidas) => MesclarRespostas(Respostas, recebidas ?? throw new ArgumentNullException(nameof(recebidas)));
    public IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> GruposAposMesclar(IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> recebidos)
    {
        ArgumentNullException.ThrowIfNull(recebidos);
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> resultado = new(Grupos, StringComparer.Ordinal);
        foreach ((string codigo, IReadOnlyList<OcorrenciaDeGrupoInscricao> ocorrencias) in recebidos) resultado[codigo] = ocorrencias;
        return resultado;
    }
    public PreenchimentoDoCandidato CanonizarRespostas(IReadOnlyDictionary<string, JsonElement> recebidas, IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes) =>
        ComRespostas(Canonizar(MesclarRespostas(Respostas, recebidas ?? throw new ArgumentNullException(nameof(recebidas))), definicoes));
    public PreenchimentoDoCandidato CanonizarGrupos(IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> recebidos, IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes) =>
        ComGrupos(TransformarGrupos(GruposAposMesclar(recebidos), r => Canonizar(MesclarRespostas(new Dictionary<string, JsonElement>(StringComparer.Ordinal), r), definicoes)));
    public PreenchimentoDoCandidato FiltrarRespostas(IReadOnlyDictionary<string, FatoResolvido> fatos) => ComRespostas(RespostasAplicaveis(Respostas, fatos));
    public PreenchimentoDoCandidato FiltrarGrupos(IReadOnlyList<AvaliacaoGrupo> grupos) => ComGrupos(GruposAplicaveis(grupos));
    public static Dictionary<string, JsonElement> Mesclar(IReadOnlyDictionary<string, JsonElement> atual, IReadOnlyDictionary<string, JsonElement> recebida) => MesclarRespostas(atual, recebida ?? throw new ArgumentNullException(nameof(recebida)));

    public PreenchimentoDoCandidato ComRespostasAdmitidas(RespostasAdmitidas admitidas) =>
        ComRespostas(Respostas.Where(r => admitidas.DoNivelSuperior.Contains(r.Key))
            .ToDictionary(static r => r.Key, static r => r.Value, StringComparer.Ordinal));

    public PreenchimentoDoCandidato ComGruposAdmitidos(RespostasAdmitidas admitidas) =>
        ComGrupos(Grupos.Where(g => admitidas.SubitensPorGrupo.ContainsKey(g.Key)).ToDictionary(
            static g => g.Key, g => (IReadOnlyList<OcorrenciaDeGrupoInscricao>)[.. g.Value.Select(o => o with
            {
                Respostas = o.Respostas.Where(r => admitidas.SubitensPorGrupo[g.Key].Contains(r.Key))
                    .ToDictionary(static r => r.Key, static r => r.Value, StringComparer.Ordinal),
            })], StringComparer.Ordinal));

    public PreenchimentoDoCandidato ComAceitesAdmitidos(RespostasAdmitidas admitidas) =>
        ComAceites([.. Aceites.Where(a => admitidas.Termos.TryGetValue(new TermoNoFormulario(a.FormularioCodigo, a.Codigo), out TermoAdmitido? termo)
            && a.ValeParaAVersao(termo))]);

    public bool Equals(PreenchimentoDoCandidato? outro) => outro is not null && Opcoes == outro.Opcoes
        && RespostasIguais(Respostas, outro.Respostas) && GruposIguais(outro.Grupos) && Aceites.SequenceEqual(outro.Aceites);

    public override bool Equals(object? obj) => obj is PreenchimentoDoCandidato outro && Equals(outro);

    public override int GetHashCode() => HashCode.Combine(Opcoes, Respostas.Count, Grupos.Count, Aceites.Count);

    private static bool RespostasIguais(IReadOnlyDictionary<string, JsonElement>? primeira, IReadOnlyDictionary<string, JsonElement>? segunda) =>
        ReferenceEquals(primeira, segunda) || (primeira is not null && segunda is not null && primeira.Count == segunda.Count && primeira.All(r => segunda.TryGetValue(r.Key, out JsonElement valor) && JsonElement.DeepEquals(r.Value, valor)));

    private bool GruposIguais(IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> outros) =>
        Grupos.Count == outros.Count && Grupos.All(g => outros.TryGetValue(g.Key, out IReadOnlyList<OcorrenciaDeGrupoInscricao>? ocorrencias)
            && OcorrenciasIguais(g.Value, ocorrencias));

    private static bool OcorrenciasIguais(IReadOnlyList<OcorrenciaDeGrupoInscricao>? primeira, IReadOnlyList<OcorrenciaDeGrupoInscricao>? segunda) =>
        ReferenceEquals(primeira, segunda) || (primeira is not null && segunda is not null && primeira.Count == segunda.Count
            && primeira.Zip(segunda).All(par => ReferenceEquals(par.First, par.Second) || (par.First is not null && par.Second is not null
                && par.First.Id == par.Second.Id && RespostasIguais(par.First.Respostas, par.Second.Respostas))));


    public PreenchimentoDoCandidato MescladoCom(PreenchimentoDoCandidato recebido)
    {
        ArgumentNullException.ThrowIfNull(recebido);
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = new(Grupos, StringComparer.Ordinal);
        foreach ((string codigo, IReadOnlyList<OcorrenciaDeGrupoInscricao> ocorrencias) in recebido.Grupos)
        {
            grupos[codigo] = ocorrencias;
        }
        return Criar(recebido.Opcoes, MesclarRespostas(Respostas, recebido.Respostas), grupos, recebido.Aceites);
    }

    private static Dictionary<string, JsonElement> MesclarRespostas(IReadOnlyDictionary<string, JsonElement> atual, IReadOnlyDictionary<string, JsonElement> recebida)
    {
        Dictionary<string, JsonElement> mesclado = new(atual, StringComparer.Ordinal);
        foreach ((string codigo, JsonElement valor) in recebida)
        {
            if (valor.ValueKind == JsonValueKind.Null) { mesclado.Remove(codigo); }
            else { mesclado[codigo] = valor; }
        }
        return mesclado;
    }

    public PreenchimentoDoCandidato NaFormaCanonica(IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes) =>
        Criar(Opcoes, Canonizar(Respostas, definicoes), TransformarGrupos(Grupos, r => Canonizar(r, definicoes)), Aceites);

    private static Dictionary<string, JsonElement> Canonizar(IReadOnlyDictionary<string, JsonElement> respostas, IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes) =>
        respostas.ToDictionary(static r => r.Key, r => definicoes.TryGetValue(r.Key, out DefinicaoDaResposta? definicao) ? definicao.NaFormaCanonica(r.Value) : r.Value, StringComparer.Ordinal);

    public PreenchimentoDoCandidato SemValoresNulos() =>
        Criar(Opcoes, MesclarRespostas(new Dictionary<string, JsonElement>(StringComparer.Ordinal), Respostas),
            TransformarGrupos(Grupos, static r => MesclarRespostas(new Dictionary<string, JsonElement>(StringComparer.Ordinal), r)), Aceites);

    private static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> TransformarGrupos(
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos, Func<IReadOnlyDictionary<string, JsonElement>, IReadOnlyDictionary<string, JsonElement>> transformar) =>
        grupos.ToDictionary(static g => g.Key, g => (IReadOnlyList<OcorrenciaDeGrupoInscricao>)[.. g.Value.Select(o => o with { Respostas = transformar(o.Respostas) })], StringComparer.Ordinal);

    public PreenchimentoDoCandidato QueSeAplicam(IReadOnlyDictionary<string, FatoResolvido> fatos, IReadOnlyList<AvaliacaoGrupo> grupos) =>
        Criar(Opcoes, RespostasAplicaveis(Respostas, fatos), GruposAplicaveis(grupos), Aceites);

    private static Dictionary<string, JsonElement> RespostasAplicaveis(IReadOnlyDictionary<string, JsonElement> respostas, IReadOnlyDictionary<string, FatoResolvido> fatos) =>
        respostas.Where(r => !(fatos.TryGetValue(r.Key, out FatoResolvido? fato) && fato.Estado == EstadoFato.NaoAplicavel))
            .ToDictionary(static r => r.Key, static r => r.Value, StringComparer.Ordinal);

    private Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> GruposAplicaveis(IReadOnlyList<AvaliacaoGrupo> grupos)
    {
        Dictionary<string, AvaliacaoGrupo> avaliados = grupos.ToDictionary(static g => g.Codigo, StringComparer.Ordinal);
        return Grupos.Where(g => !avaliados.TryGetValue(g.Key, out AvaliacaoGrupo? a) || a.Estado != EstadoFato.NaoAplicavel)
            .ToDictionary(static g => g.Key, g => avaliados.TryGetValue(g.Key, out AvaliacaoGrupo? a) ? OcorrenciasAplicaveis(g.Value, a) : g.Value, StringComparer.Ordinal);
    }

    private static IReadOnlyList<OcorrenciaDeGrupoInscricao> OcorrenciasAplicaveis(IReadOnlyList<OcorrenciaDeGrupoInscricao> ocorrencias, AvaliacaoGrupo grupo)
    {
        Dictionary<string, AvaliacaoOcorrencia> avaliadas = grupo.Ocorrencias.ToDictionary(static o => o.Id, StringComparer.Ordinal);
        return [.. ocorrencias.Select(o => avaliadas.TryGetValue(o.Id, out AvaliacaoOcorrencia? a) ? o with { Respostas = RespostasAplicaveis(o.Respostas, a.Fatos) } : o)];
    }

    public IReadOnlyList<FieldError> RecusasDeForma(RespostasAdmitidas admitidas)
        => RecusasDeForma(Respostas, Grupos, admitidas);

    public static IReadOnlyList<FieldError> RecusasDeForma(IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos, RespostasAdmitidas admitidas)
    {
        ArgumentNullException.ThrowIfNull(respostas);
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(admitidas);
        bool SoDeGrupo(string fato) => admitidas.Definicoes.ContainsKey(fato) && !admitidas.DoNivelSuperior.Contains(fato);
        List<FieldError> recusas = [.. respostas.Keys.Where(SoDeGrupo).Select(fato => new FieldError(fato, new DomainError(
            InscricaoErrorCodes.FatoSoDeGrupo, "Este campo só pode ser respondido dentro de uma ocorrência do grupo. Remova-o das respostas de nível superior e informe-o na ocorrência.")))];
        recusas.AddRange(ConferirValores(respostas.Where(r => !SoDeGrupo(r.Key)).ToDictionary(static r => r.Key, static r => r.Value, StringComparer.Ordinal), grupos, admitidas.Definicoes, admitidas.SubitensPorGrupo));
        return recusas;
    }

    public static List<FieldError> ConferirValores(IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos, IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes,
        IReadOnlyDictionary<string, IReadOnlySet<string>> subitensPorGrupo)
    {
        ArgumentNullException.ThrowIfNull(respostas);
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(definicoes);
        ArgumentNullException.ThrowIfNull(subitensPorGrupo);
        List<FieldError> recusas = [.. RecusasDeValor(respostas, definicoes, string.Empty)];
        foreach ((string codigo, IReadOnlyList<OcorrenciaDeGrupoInscricao> ocorrencias) in grupos)
        {
            recusas.AddRange(RecusasDoGrupo(codigo, ocorrencias, definicoes, subitensPorGrupo));
        }
        return recusas;
    }

    private static IEnumerable<FieldError> RecusasDoGrupo(string codigo, IReadOnlyList<OcorrenciaDeGrupoInscricao> ocorrencias,
        IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes, IReadOnlyDictionary<string, IReadOnlySet<string>> subitensPorGrupo)
    {
        if (!subitensPorGrupo.TryGetValue(codigo, out IReadOnlySet<string>? subitens))
        {
            return [new FieldError(codigo, new DomainError(InscricaoErrorCodes.GrupoForaDoFormulario,
                "Este grupo não pertence ao formulário desta inscrição. Remova-o das respostas."))];
        }
        HashSet<string> ids = new(StringComparer.Ordinal);
        return [.. ocorrencias.SelectMany(o => RecusasDaOcorrencia(codigo, o, ids, subitens, definicoes))];
    }

    private static IEnumerable<FieldError> RecusasDaOcorrencia(string grupo, OcorrenciaDeGrupoInscricao ocorrencia, HashSet<string> ids,
        IReadOnlySet<string> subitens, IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes)
    {
        if (string.IsNullOrWhiteSpace(ocorrencia.Id) || !ids.Add(ocorrencia.Id))
        {
            return [new FieldError($"{grupo}.{ocorrencia.Id}", new DomainError(InscricaoErrorCodes.GrupoIdDuplicado,
                "Cada ocorrência do grupo precisa de um identificador próprio, não vazio e não repetido."))];
        }
        string prefixo = $"{grupo}.{ocorrencia.Id}.";
        return ocorrencia.Respostas.Keys.Where(fato => !subitens.Contains(fato)).Select(fato => new FieldError(prefixo + fato,
            new DomainError(InscricaoErrorCodes.FatoForaDoGrupo, "Este campo não pertence ao grupo. Remova-o desta ocorrência e responda-o fora do grupo, se o formulário o pedir.")))
            .Concat(RecusasDeValor(ocorrencia.Respostas.Where(r => subitens.Contains(r.Key)).ToDictionary(static r => r.Key, static r => r.Value, StringComparer.Ordinal), definicoes, prefixo));
    }

    private static IEnumerable<FieldError> RecusasDeValor(IReadOnlyDictionary<string, JsonElement> respostas, IReadOnlyDictionary<string, DefinicaoDaResposta> definicoes, string prefixo)
    {
        foreach ((string codigo, JsonElement valor) in respostas)
        {
            if (!definicoes.TryGetValue(codigo, out DefinicaoDaResposta? definicao))
                yield return new FieldError(prefixo + codigo, new DomainError(InscricaoErrorCodes.FatoForaDoFormulario, "Este campo não pertence ao formulário desta inscrição."));
            else if (valor.ValueKind != JsonValueKind.Null && !definicao.Aceita(valor))
                yield return new FieldError(prefixo + codigo, new DomainError(InscricaoErrorCodes.ValorForaDoTipo, "A resposta não tem o tipo que o campo pede."));
        }
    }

    public IEnumerable<FieldError> RecusasDoEndereco(RespostasAdmitidas admitidas) =>
        EnderecosRecusados(admitidas ?? throw new ArgumentNullException(nameof(admitidas)));

    private IEnumerable<FieldError> EnderecosRecusados(RespostasAdmitidas admitidas) =>
        admitidas.Definicoes.Where(d => d.Value.Tipo == TipoDoValorDeResposta.Endereco && Respostas.ContainsKey(d.Key))
            .SelectMany(d => RecusasDoEnderecoDeclarado(d.Key, Respostas[d.Key]));

    private static IEnumerable<FieldError> RecusasDoEnderecoDeclarado(string codigo, JsonElement valor)
    {
        if (!EnderecoDeclarado.TentarLer(valor, out EnderecoDeclarado endereco) || endereco.EmBranco) return [];
        return RecusasDoCep(codigo, endereco).Concat(RecusasDoComplemento(codigo, endereco));
    }

    private static IEnumerable<FieldError> RecusasDoCep(string codigo, EnderecoDeclarado endereco) =>
        endereco.CepSomenteDigitos is null ? [new FieldError(codigo, new DomainError(InscricaoErrorCodes.EnderecoSemCep,
            "Informe o CEP do seu endereço, com 8 dígitos. Se você não sabe o CEP, consulte a busca dos Correios; se o seu bairro não tem CEP próprio, use o CEP geral da sua cidade e descreva o endereço real no complemento."))] : [];

    private static IEnumerable<FieldError> RecusasDoComplemento(string codigo, EnderecoDeclarado endereco) =>
        endereco.Complemento is { Length: > EnderecoDeclarado.ComplementoMaxLength } ? [new FieldError(codigo, new DomainError(InscricaoErrorCodes.EnderecoComplementoLongo,
            $"O complemento aceita até {EnderecoDeclarado.ComplementoMaxLength} caracteres. Resuma a descrição do seu endereço."))] : [];

    public IEnumerable<FieldError> RecusasDosAceites(IReadOnlyDictionary<TermoNoFormulario, TermoAdmitido> termos) =>
        ConferirAceites(termos ?? throw new ArgumentNullException(nameof(termos)));

    private IEnumerable<FieldError> ConferirAceites(IReadOnlyDictionary<TermoNoFormulario, TermoAdmitido> termos)
    {
        HashSet<string> vistos = new(StringComparer.Ordinal);
        for (int indice = 0; indice < Aceites.Count; indice++)
        {
            FieldError? recusa = RecusaDoAceite(Aceites[indice], indice, vistos, termos);
            if (recusa is not null) yield return recusa;
        }
    }

    private static FieldError? RecusaDoAceite(AceiteDeTermo aceite, int indice, HashSet<string> vistos, IReadOnlyDictionary<TermoNoFormulario, TermoAdmitido> termos)
    {
        string campo = $"aceites[{indice}]";
        if (!vistos.Add(aceite.Codigo)) return new FieldError(campo, new DomainError(InscricaoErrorCodes.AceiteRepetido,
            $"O termo '{aceite.Codigo}' foi aceito mais de uma vez. Envie um único aceite para cada termo."));
        if (!termos.TryGetValue(new TermoNoFormulario(aceite.FormularioCodigo, aceite.Codigo), out TermoAdmitido? termo)) return new FieldError(campo, new DomainError(InscricaoErrorCodes.AceiteForaDoFormulario,
            $"O termo '{aceite.Codigo}' não faz parte do formulário. Recarregue a página e aceite os termos exibidos."));
        return aceite.ValeParaAVersao(termo) ? null : new FieldError(campo, new DomainError(InscricaoErrorCodes.AceiteDeOutraVersao,
            $"O aceite do termo '{aceite.Codigo}' não é da versão que o formulário apresenta. Recarregue a página e aceite o termo exibido."));
    }
}
