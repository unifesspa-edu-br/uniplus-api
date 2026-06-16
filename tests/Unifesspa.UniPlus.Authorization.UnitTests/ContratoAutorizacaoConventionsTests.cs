namespace Unifesspa.UniPlus.Authorization.UnitTests;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.ValueObjects;

/// <summary>
/// Fitness de contrato (Story #608, task #653) sobre os tipos formais do módulo
/// <c>Unifesspa.UniPlus.Authorization</c>. Trava por <b>reflection</b> as
/// invariantes estruturais do contrato inteiro — depois que todos os tipos
/// existem (T1 #651 + T2 #652) — protegendo contra regressão silenciosa:
/// <list type="bullet">
///   <item>CA-11 — setter público introduzido ou coleção mutável exposta
///   (propriedade <i>ou</i> campo);</item>
///   <item>CA-10 — campo canônico omitido/renomeado, opcionalidade trocada ou
///   snapshot acoplado à entidade persistida;</item>
///   <item>CA-07 — nome de provedor de identidade embutido (em qualquer membro
///   público) ou subject do token tipado como <see cref="Guid"/>.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// Espelha o padrão de <c>ForensicEntityConventionsTests</c> (ArchTests): a
/// regra é estrutural e a varredura por reflection sobre o assembly de contrato
/// é mais legível que o DSL de ArchUnitNET para este conjunto de invariantes.
/// </para>
/// <para>
/// O conjunto inspecionado segue o assembly de
/// <see cref="AuthorizationSubject"/> — qualquer <i>record</i> novo no módulo
/// (classe <b>ou</b> struct) entra automaticamente na varredura. <b>Escopo
/// deliberado:</b> esta fitness cobre apenas os tipos do contrato novo
/// (ADR-0078); o fitness que bloqueia o modelo de acesso anterior é da Story
/// #648, fora daqui.
/// </para>
/// </remarks>
public sealed class ContratoAutorizacaoConventionsTests
{
    // Interfaces de coleção somente-leitura aceitas como exposição imutável.
    // Array, coleções concretas (List<>, HashSet<>) e interfaces mutáveis
    // (IList<>, ICollection<>, ISet<>, IDictionary<,>) são reprovadas.
    private static readonly Type[] ColecoesSomenteLeituraPermitidas =
    [
        typeof(IReadOnlyList<>),
        typeof(IReadOnlyCollection<>),
        typeof(IReadOnlySet<>),
        typeof(IReadOnlyDictionary<,>),
        typeof(IEnumerable<>),
    ];

    // Blocklist curada dos provedores de identidade que o Uni+ integra ou
    // poderia integrar. O contrato (ADR-0078) usa o emissor genérico do token,
    // sem amarração a produto.
    private static readonly string[] MarcasDeProvedor =
        ["keycloak", "govbr", "gov_br", "okta", "auth0", "azuread", "cognito", "pingid", "onelogin", "adfs"];

    [Fact(DisplayName = "CA-11: nenhum tipo do contrato tem setter público nem coleção mutável exposta")]
    public void TiposFormais_SemSetterPublicoNemColecaoMutavel()
    {
        IReadOnlyList<Type> contrato = LocalizarTiposDeContrato();

        contrato.Should().NotBeEmpty(
            "T1 + T2 (#651/#652) introduzem os records do contrato; ausência "
            + "indica que a varredura perdeu o assembly de autorização.");

        List<string> violacoes = [];

        foreach (Type tipo in contrato)
        {
            foreach (PropertyInfo prop in tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.SetMethod is { IsPublic: true } setter && !EhInitOnly(setter))
                {
                    violacoes.Add($"{tipo.Name}.{prop.Name} (setter público mutável)");
                }

                if (EhColecao(prop.PropertyType) && !EhColecaoSomenteLeitura(prop.PropertyType))
                {
                    violacoes.Add($"{tipo.Name}.{prop.Name} (coleção mutável exposta: {prop.PropertyType})");
                }
            }

            // Campos: um campo público reatribuível é mutação direta; um campo
            // público readonly de coleção concreta ainda expõe mutação do
            // conteúdo (Add/Remove). Ambos reprovam. (CA1051 já barra campo
            // público de instância no build — esta é defesa em profundidade.)
            foreach (FieldInfo campo in tipo.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!campo.IsInitOnly)
                {
                    violacoes.Add($"{tipo.Name}.{campo.Name} (campo de instância público mutável)");
                }
                else if (EhColecao(campo.FieldType) && !EhColecaoSomenteLeitura(campo.FieldType))
                {
                    violacoes.Add($"{tipo.Name}.{campo.Name} (campo readonly de coleção mutável: {campo.FieldType})");
                }
            }
        }

        violacoes.Should().BeEmpty(
            "CA-11: os tipos do contrato são imutáveis — sem setter público, sem "
            + "coleção mutável exposta (use IReadOnly*) e sem campo de instância "
            + "mutável; mutação pós-construção quebra a igualdade por valor e a "
            + "não-falsificabilidade do sujeito.");
    }

    [Fact(DisplayName = "CA-10: schema canônico do sujeito completo (tipo + opcionalidade) e snapshots desacoplados")]
    public void AuthorizationSubject_SchemaCanonicoCompleto_SnapshotsDesacoplados()
    {
        // Schema canônico fechado do AuthorizationSubject: os 8 campos, com o tipo
        // exato e a opcionalidade de cada um. Omitir, renomear, acrescentar campo
        // ou trocar a nulabilidade reprova.
        Dictionary<string, (Type Tipo, bool Anulavel)> schemaSujeito = new(StringComparer.Ordinal)
        {
            [nameof(AuthorizationSubject.Usuario)] = (typeof(UsuarioRef), false),
            [nameof(AuthorizationSubject.GruposOidc)] = (typeof(IReadOnlySet<string>), false),
            [nameof(AuthorizationSubject.UnidadesAdministradas)] = (typeof(IReadOnlySet<Guid>), false),
            [nameof(AuthorizationSubject.EscoposAuditoria)] = (typeof(IReadOnlyList<EscopoAuditoriaVigente>), false),
            [nameof(AuthorizationSubject.AtuacaoAtiva)] = (typeof(AtuacaoVigente), true),
            [nameof(AuthorizationSubject.MfaSatisfeito)] = (typeof(bool), false),
            [nameof(AuthorizationSubject.Jti)] = (typeof(string), false),
            [nameof(AuthorizationSubject.ConcessoesEfetivas)] = (typeof(IReadOnlyList<EffectiveGrant>), false),
        };

        PropriedadesPublicas(typeof(AuthorizationSubject)).Should().BeEquivalentTo(
            schemaSujeito.Keys,
            "CA-10: o schema canônico do AuthorizationSubject é fechado — omitir, "
            + "renomear ou acrescentar um campo deve reprovar (prova negativa).");

        NullabilityInfoContext nulabilidade = new();
        foreach ((string nome, (Type tipoEsperado, bool anulavel)) in schemaSujeito)
        {
            PropertyInfo prop = PropriedadeObrigatoria(typeof(AuthorizationSubject), nome);

            prop.PropertyType.Should().Be(
                tipoEsperado,
                $"CA-10: AuthorizationSubject.{nome} deve manter o tipo canônico ({tipoEsperado}).");

            nulabilidade.Create(prop).ReadState.Should().Be(
                anulavel ? NullabilityState.Nullable : NullabilityState.NotNull,
                $"CA-10: AuthorizationSubject.{nome} deve manter a opcionalidade canônica "
                + $"({(anulavel ? "anulável" : "obrigatória")}).");
        }

        // PermissionRequirement: os dois campos canônicos que a Story #608 marca
        // como sob risco de omissão/acoplamento.
        PropriedadeObrigatoria(typeof(PermissionRequirement), nameof(PermissionRequirement.BaseLegalPadrao))
            .PropertyType.Should().Be<string>(
                "CA-10: PermissionRequirement expõe BaseLegalPadrao (consumida pela verificação de base legal).");
        PropriedadeObrigatoria(typeof(PermissionRequirement), nameof(PermissionRequirement.EscopoContextoObrigatorio))
            .PropertyType.Should().Be<IReadOnlyList<string>>(
                "CA-10: PermissionRequirement expõe EscopoContextoObrigatorio (campos de contexto exigidos).");

        // Snapshots desacoplados: EscoposAuditoria/AtuacaoAtiva são tipados pelos
        // VOs de contrato do próprio módulo (já garantido pelo schema acima);
        // estes VOs vivem no assembly de contrato, nunca numa entidade persistida
        // (que viveria em outro assembly de domínio). Acoplar à entidade reprova.
        Assembly contratoAssembly = typeof(AuthorizationSubject).Assembly;
        typeof(EscopoAuditoriaVigente).Assembly.Should().BeSameAs(
            contratoAssembly,
            "CA-10: EscoposAuditoria é o VO de contrato (snapshot), não a entidade persistida.");
        typeof(AtuacaoVigente).Assembly.Should().BeSameAs(
            contratoAssembly,
            "CA-10: AtuacaoAtiva é o VO de contrato (snapshot), não a entidade persistida.");
    }

    [Fact(DisplayName = "CA-07: nenhum tipo embute provedor de identidade e o subject do token é string opaca, nunca Guid")]
    public void TiposFormais_NaoEmbutemProvedorDeIdentidade()
    {
        IReadOnlyList<Type> contrato = LocalizarTiposDeContrato();

        // (1) Provedor de identidade não embutido em NENHUM identificador público
        // do contrato — nome do tipo, membros, tipos de membro e parâmetros de
        // fábrica. O emissor é genérico do token (UsuarioRef.Emissor), ADR-0078.
        List<string> embutemProvedor = [];
        foreach (Type tipo in contrato)
        {
            foreach (string identificador in ColetarIdentificadores(tipo))
            {
                foreach (string marca in MarcasDeProvedor.Where(m => identificador.Contains(m, StringComparison.OrdinalIgnoreCase)))
                {
                    embutemProvedor.Add($"{tipo.Name}: '{identificador}' embute o provedor '{marca}'");
                }
            }
        }

        embutemProvedor.Should().BeEmpty(
            "CA-07 (ADR-0078): nenhum tipo nomeia um provedor de identidade específico — "
            + "o emissor é genérico do token, sem amarração a produto.");

        // (2) Subject e emissor do token são string opaca OBRIGATÓRIA, jamais
        // Guid: identificadores de provedores externos não têm forma de GUID, e
        // tipá-los como Guid (ou torná-los anuláveis) rejeitaria/enfraqueceria
        // sujeitos válidos (ADR-0032 mantém OIDC/token como strings).
        NullabilityInfoContext nulabilidade = new();

        PropertyInfo subject = PropriedadeObrigatoria(typeof(UsuarioRef), nameof(UsuarioRef.Subject));
        subject.PropertyType.Should().Be<string>(
            "CA-07: o subject do token é string opaca — tratá-lo como Guid quebraria sujeitos válidos.");
        nulabilidade.Create(subject).ReadState.Should().Be(
            NullabilityState.NotNull, "CA-07: o subject do token é obrigatório, não anulável.");

        PropertyInfo emissor = PropriedadeObrigatoria(typeof(UsuarioRef), nameof(UsuarioRef.Emissor));
        emissor.PropertyType.Should().Be<string>("CA-07: o emissor é string genérica do token.");
        nulabilidade.Create(emissor).ReadState.Should().Be(
            NullabilityState.NotNull, "CA-07: o emissor do token é obrigatório, não anulável.");

        // (3) Nenhum membro de dados (propriedade ou campo) do contrato expõe o
        // subject do token tipado como Guid — o sujeito é explícito e
        // não-falsificável (OWASP API 2023, BFLA).
        List<string> subjectComoGuid = [];
        foreach (Type tipo in contrato)
        {
            foreach ((string nome, Type tipoMembro) in MembrosDeDados(tipo))
            {
                bool pareceSubjectDoToken =
                    nome.Contains("subject", StringComparison.OrdinalIgnoreCase)
                    || nome.Equals("Sub", StringComparison.OrdinalIgnoreCase);

                // Recorre em coleções/arrays: IReadOnlySet<Guid> Subjects também
                // expõe o subject do token como Guid interno.
                bool exporSubjectComoGuid = TiposContidos(tipoMembro).Any(t => TipoSubjacente(t) == typeof(Guid));

                if (pareceSubjectDoToken && exporSubjectComoGuid)
                {
                    subjectComoGuid.Add($"{tipo.Name}.{nome}");
                }
            }
        }

        subjectComoGuid.Should().BeEmpty(
            "CA-07: o subject do token nunca é tratado como chave interna (Guid).");
    }

    // Tipos do contrato = todos os records públicos do assembly de autorização
    // (classe ou struct). Classes estáticas (IsAbstract) e enums ficam de fora
    // automaticamente; o detector de record cobre record class e record struct.
    private static IReadOnlyList<Type> LocalizarTiposDeContrato() =>
        [.. typeof(AuthorizationSubject).Assembly
            .GetTypes()
            .Where(t => t is { IsPublic: true, IsAbstract: false, IsEnum: false, IsInterface: false } && EhRecord(t))
            .OrderBy(t => t.Name, StringComparer.Ordinal)];

    // <Clone>$ é sintetizado para todo record class; PrintMembers(StringBuilder)
    // para record class e record struct. Ambos têm nomes que o compilador emite
    // — não há colisão com membros escritos à mão neste assembly de contrato.
    private static bool EhRecord(Type tipo) =>
        tipo.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Any(EhMembroSinteticoDeRecord);

    private static bool EhMembroSinteticoDeRecord(MethodInfo metodo) =>
        string.Equals(metodo.Name, "<Clone>$", StringComparison.Ordinal)
        || (string.Equals(metodo.Name, "PrintMembers", StringComparison.Ordinal)
            && metodo.ReturnType == typeof(bool)
            && metodo.GetParameters() is [{ ParameterType: var unico }]
            && unico == typeof(System.Text.StringBuilder));

    // Todos os identificadores públicos do tipo: nome do tipo, nomes de membros,
    // tipos de propriedades/campos e parâmetros (com tipo) de métodos/fábricas.
    private static IEnumerable<string> ColetarIdentificadores(Type tipo)
    {
        yield return tipo.Name;

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (MemberInfo membro in tipo.GetMembers(flags))
        {
            yield return membro.Name;

            switch (membro)
            {
                case PropertyInfo prop:
                    foreach (string nomeTipo in NomesDeTipo(prop.PropertyType))
                    {
                        yield return nomeTipo;
                    }

                    break;
                case FieldInfo campo:
                    foreach (string nomeTipo in NomesDeTipo(campo.FieldType))
                    {
                        yield return nomeTipo;
                    }

                    break;
                case MethodBase metodo:
                    foreach (ParameterInfo parametro in metodo.GetParameters())
                    {
                        if (parametro.Name is { } nomeParametro)
                        {
                            yield return nomeParametro;
                        }

                        foreach (string nomeTipo in NomesDeTipo(parametro.ParameterType))
                        {
                            yield return nomeTipo;
                        }
                    }

                    break;
                default:
                    break;
            }
        }
    }

    private static IEnumerable<string> NomesDeTipo(Type tipo) => TiposContidos(tipo).Select(t => t.Name);

    // O tipo e, recursivamente, os tipos que ele contém — argumentos genéricos e
    // elemento de array. Garante que um provedor de identidade embutido atrás de
    // um genérico (ex.: IReadOnlyList<KeycloakGroup>) ou de array não escape à
    // varredura, e que um subject tipado como Guid dentro de coleção seja visto.
    private static IEnumerable<Type> TiposContidos(Type tipo)
    {
        yield return tipo;

        if (tipo.IsArray && tipo.GetElementType() is { } elemento)
        {
            foreach (Type contido in TiposContidos(elemento))
            {
                yield return contido;
            }
        }

        if (tipo.IsGenericType)
        {
            foreach (Type argumento in tipo.GetGenericArguments())
            {
                foreach (Type contido in TiposContidos(argumento))
                {
                    yield return contido;
                }
            }
        }
    }

    private static IEnumerable<(string Nome, Type Tipo)> MembrosDeDados(Type tipo)
    {
        foreach (PropertyInfo prop in tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            yield return (prop.Name, prop.PropertyType);
        }

        foreach (FieldInfo campo in tipo.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            yield return (campo.Name, campo.FieldType);
        }
    }

    private static IEnumerable<string> PropriedadesPublicas(Type tipo) =>
        tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name);

    private static PropertyInfo PropriedadeObrigatoria(Type tipo, string nome)
    {
        PropertyInfo? prop = tipo.GetProperty(nome, BindingFlags.Public | BindingFlags.Instance);
        prop.Should().NotBeNull(
            $"o tipo {tipo.Name} deve expor a propriedade canônica {nome} (prova negativa de completude).");
        return prop!;
    }

    // Um setter é init-only (não muta pós-construção) quando o seu parâmetro de
    // retorno carrega o modreq IsExternalInit emitido pelo compilador para `init`.
    private static bool EhInitOnly(MethodInfo setter) =>
        setter.ReturnParameter.GetRequiredCustomModifiers()
            .Any(t => string.Equals(t.FullName, "System.Runtime.CompilerServices.IsExternalInit", StringComparison.Ordinal));

    private static bool EhColecao(Type tipo) =>
        tipo != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(tipo);

    private static bool EhColecaoSomenteLeitura(Type tipo)
    {
        if (tipo.IsArray || !tipo.IsGenericType)
        {
            return false;
        }

        return ColecoesSomenteLeituraPermitidas.Contains(tipo.GetGenericTypeDefinition());
    }

    private static Type TipoSubjacente(Type tipo) => Nullable.GetUnderlyingType(tipo) ?? tipo;
}
