param([string]$Type, [string]$Method, [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld')
$ErrorActionPreference = 'Stop'
[Runtime.Loader.AssemblyLoadContext]::Default.add_Resolving([Func[Runtime.Loader.AssemblyLoadContext,Reflection.AssemblyName,Reflection.Assembly]]({param($context,$name) $path=Join-Path $GamePath ($name.Name+'.dll'); if(Test-Path -LiteralPath $path){return $context.LoadFromAssemblyPath($path)}; return $null}.GetNewClosure()))
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $GamePath 'Assembly-CSharp.dll'))
$opcodes=@{}; [Reflection.Emit.OpCodes].GetFields([Reflection.BindingFlags]'Public,Static') | ForEach-Object { $op=$_.GetValue($null); $opcodes[[int]$op.Value -band 65535]=$op }
$members=$assembly.GetType($Type).GetMethods([Reflection.BindingFlags]'Public,NonPublic,Static,Instance,DeclaredOnly') | Where-Object Name -eq $Method
foreach($member in $members){
    $member.ToString(); $bytes=$member.GetMethodBody().GetILAsByteArray(); $offset=0
    while($offset -lt $bytes.Length){
        $start=$offset; $value=[int]$bytes[$offset++]; if($value -eq 254){$value=0xFE00+[int]$bytes[$offset++]}; $op=$opcodes[$value]; $operand=''
        switch($op.OperandType.ToString()){
            'InlineNone' {}
            {$_ -in 'ShortInlineI','ShortInlineBrTarget','ShortInlineVar'} {$operand=$bytes[$offset++]; break}
            'InlineVar' {$operand=[BitConverter]::ToUInt16($bytes,$offset);$offset+=2}
            'InlineI8' {$operand=[BitConverter]::ToInt64($bytes,$offset);$offset+=8}
            'InlineR' {$operand=[BitConverter]::ToDouble($bytes,$offset);$offset+=8}
            'ShortInlineR' {$operand=[BitConverter]::ToSingle($bytes,$offset);$offset+=4}
            'InlineSwitch' {$count=[BitConverter]::ToInt32($bytes,$offset);$offset+=4+4*$count;$operand="switch $count"}
            default {$token=[BitConverter]::ToInt32($bytes,$offset);$offset+=4;try{if($op.OperandType.ToString() -eq 'InlineString'){$operand='"'+$member.Module.ResolveString($token)+'"'}elseif($op.OperandType.ToString() -in 'InlineMethod','InlineField','InlineType','InlineTok'){$operand=$member.Module.ResolveMember($token)}else{$operand=$token}}catch{$operand=$token}}
        }
        '{0:x4}: {1} {2}' -f $start,$op.Name,$operand
    }
}
