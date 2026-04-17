•
XD:\yusuf\Desktop\Student\YapayZekaDYG\KahootProjesi\KahootClone.Infrastructure\Class1.cs
	namespace 	
KahootClone
 
. 
Infrastructure $
;$ %
public 
class 
Class1 
{ 
} ×
eD:\yusuf\Desktop\Student\YapayZekaDYG\KahootProjesi\KahootClone.Infrastructure\Data\MongoDbContext.cs
	namespace 	
KahootClone
 
. 
Infrastructure $
.$ %
Data% )
;) *
public 
class 
MongoDbContext 
{ 
private		 
readonly		 
IMongoDatabase		 #
	_database		$ -
;		- .
public 

MongoDbContext 
( 
string  
connectionString! 1
,1 2
string3 9
databaseName: F
)F G
{ 
var 
client 
= 
new 
MongoClient $
($ %
connectionString% 5
)5 6
;6 7
	_database 
= 
client 
. 
GetDatabase &
(& '
databaseName' 3
)3 4
;4 5
} 
public 

IMongoCollection 
< 
Quiz  
>  !
Quizzes" )
=>* ,
	_database- 6
.6 7
GetCollection7 D
<D E
QuizE I
>I J
(J K
$strK T
)T U
;U V
public 

IMongoCollection 
< 
Player "
>" #
Players$ +
=>, .
	_database/ 8
.8 9
GetCollection9 F
<F G
PlayerG M
>M N
(N O
$strO X
)X Y
;Y Z
} æ
mD:\yusuf\Desktop\Student\YapayZekaDYG\KahootProjesi\KahootClone.Infrastructure\Repositories\QuizRepository.cs
	namespace 	
KahootClone
 
. 
Infrastructure $
.$ %
Repositories% 1
;1 2
public 
class 
QuizRepository 
: 
IQuizRepository -
{		 
private

 
readonly

 
MongoDbContext

 #
_context

$ ,
;

, -
public 

QuizRepository 
( 
MongoDbContext (
context) 0
)0 1
{ 
_context 
= 
context 
; 
} 
public 

void 
Add 
( 
Quiz 
quiz 
) 
{ 
_context 
. 
Quizzes 
. 
	InsertOne "
(" #
quiz# '
)' (
;( )
} 
public 

Quiz 
? 
GetByPin 
( 
string  
pin! $
)$ %
{ 
return 
_context 
. 
Quizzes 
.  
Find  $
($ %
q% &
=>' )
q* +
.+ ,
Pin, /
==0 2
pin3 6
)6 7
.7 8
FirstOrDefault8 F
(F G
)G H
;H I
} 
public 

void 
Update 
( 
Quiz 
quiz  
)  !
{ 
_context   
.   
Quizzes   
.   

ReplaceOne   #
(  # $
q  $ %
=>  & (
q  ) *
.  * +
Id  + -
==  . 0
quiz  1 5
.  5 6
Id  6 8
,  8 9
quiz  : >
)  > ?
;  ? @
}!! 
}"" Á
jD:\yusuf\Desktop\Student\YapayZekaDYG\KahootProjesi\KahootClone.Infrastructure\Settings\MongoDbSettings.cs
	namespace 	
KahootClone
 
. 
Infrastructure $
.$ %
Settings% -
;- .
public 
class 
MongoDbSettings 
{ 
public 

string 
ConnectionString "
{# $
get% (
;( )
set* -
;- .
}/ 0
=1 2
string3 9
.9 :
Empty: ?
;? @
public		 

string		 
DatabaseName		 
{		  
get		! $
;		$ %
set		& )
;		) *
}		+ ,
=		- .
string		/ 5
.		5 6
Empty		6 ;
;		; <
}

 